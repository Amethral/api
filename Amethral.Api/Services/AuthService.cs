using Microsoft.EntityFrameworkCore;
using Amethral.Api.Data;
using Amethral.Api.Data.Entities;
using Amethral.Common.DTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Amethral.Api.Services
{
    public class AuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public AuthService(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        private string GenerateWebJwt(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["JwtSettings:SecretKey"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("username", user.Username)
            };

            var token = new JwtSecurityToken(
                issuer: _config["JwtSettings:Issuer"],
                audience: _config["JwtSettings:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7), // Reste connecté 7 jours
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // 1. Génération du ticket pour Unity
        public async Task<WebTokenResponse> CreateWebTokenAsync(string deviceId)
        {
            var token = Guid.NewGuid().ToString("N"); // Simple random string
            var entity = new WebAuthToken
            {
                Token = token,
                DeviceId = deviceId,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                IsConsumed = false,
                UserId = null 
            };

            _context.WebAuthTokens.Add(entity);
            await _context.SaveChangesAsync();

            return new WebTokenResponse
            {
                Token = token,
                AuthUrl = $"{_config["FrontendUrl"]}/login?token={token}", // URL Frontend
                ExpiresAt = entity.ExpiresAt
            };
        }

        // 2. Register Email + Validation du Ticket
        // MmorpgAuth.Api/Services/AuthService.cs

        public async Task<bool> RegisterWithEmailAsync(RegisterRequest request)
        {
            // 1. Check Unicité (comme vu avant)
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                return false;

            // 2. Création de l'utilisateur (Indépendant du token)
            var user = new User
            {
                Username = request.Username,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // 3. Liaison au Token (OPTIONNEL)
            // Si le champ n'est pas vide, on tente le handshake Unity
            if (!string.IsNullOrWhiteSpace(request.WebToken))
            {
                await LinkUserToToken(request.WebToken, user.Id);
            }

            return true;
        }

        public async Task<string?> LoginWithEmailAsync(LoginRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return null;

            // Si un WebToken Unity est présent, on le lie
            if (!string.IsNullOrWhiteSpace(request.WebToken))
            {
                await LinkUserToToken(request.WebToken, user.Id);
            }

            // ON RETOURNE LE JWT POUR LE NAVIGATEUR
            return GenerateWebJwt(user);
        }

        // Méthode helper privée pour valider le ticket
        private async Task<bool> LinkUserToToken(string webToken, Guid userId)
        {
            var tokenEntity = await _context.WebAuthTokens.FirstOrDefaultAsync(t => t.Token == webToken);
            
            // Token invalide ou expiré
            if (tokenEntity == null || tokenEntity.ExpiresAt < DateTime.UtcNow || tokenEntity.IsConsumed)
                return false;

            tokenEntity.UserId = userId; // Le polling verra ça !
            await _context.SaveChangesAsync();
            return true;
        }

        // 4. Polling (Appelé par Unity)
        public async Task<AuthSuccessResponse?> FinalizeAuthAsync(string webToken, string deviceId)
        {
            var tokenEntity = await _context.WebAuthTokens
                .FirstOrDefaultAsync(t => t.Token == webToken && t.DeviceId == deviceId);

            // Pas encore prêt ou expiré
            if (tokenEntity == null || tokenEntity.UserId == null) return null;
            
            // Déjà consommé ?
            if (tokenEntity.IsConsumed) return null;

            // Consommer le token
            tokenEntity.IsConsumed = true;

            // Créer la session FishNet
            var user = await _context.Users.FindAsync(tokenEntity.UserId);
            var session = new GameSession
            {
                SessionToken = Guid.NewGuid().ToString(), // Token FishNet
                UserId = user!.Id,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            };
            
            _context.GameSessions.Add(session);
            await _context.SaveChangesAsync();

            return new AuthSuccessResponse
            {
                SessionToken = session.SessionToken,
                Username = user.Username,
                UserId = user.Id
            };
        }

        public async Task<bool> ForceLinkUserToToken(string webToken, Guid userId)
        {
            return await LinkUserToToken(webToken, userId);
        }

        public async Task<UserProfileResponse?> GetUserProfileAsync(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return null;

            return new UserProfileResponse
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                CreatedAt = user.CreatedAt
            };
        }

        // OAuth Methods

        /// <summary>
        /// Finds an existing user by OAuth provider, or creates a new user account
        /// </summary>
        public async Task<User> FindOrCreateOAuthUserAsync(string providerName, string providerKey, string email, string username)
        {
            // 1. Check if this OAuth account already exists
            var existingOAuth = await _context.UserOAuths
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.ProviderName == providerName && o.ProviderKey == providerKey);

            if (existingOAuth != null)
            {
                return existingOAuth.User;
            }

            // 2. Check if a user with this email already exists
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (existingUser != null)
            {
                // Link the OAuth provider to the existing user
                var newOAuth = new UserOAuth
                {
                    UserId = existingUser.Id,
                    ProviderName = providerName,
                    ProviderKey = providerKey
                };

                _context.UserOAuths.Add(newOAuth);
                await _context.SaveChangesAsync();

                return existingUser;
            }

            // 3. Create a new user with OAuth
            var newUser = new User
            {
                Username = username,
                Email = email,
                PasswordHash = null // OAuth users don't need a password initially
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Link OAuth to the new user
            var oauthLink = new UserOAuth
            {
                UserId = newUser.Id,
                ProviderName = providerName,
                ProviderKey = providerKey
            };

            _context.UserOAuths.Add(oauthLink);
            await _context.SaveChangesAsync();

            return newUser;
        }

        /// <summary>
        /// Links an OAuth provider to an existing logged-in user
        /// </summary>
        public async Task<bool> LinkOAuthToUserAsync(Guid userId, string providerName, string providerKey)
        {
            // Check if this OAuth is already linked to someone else
            var existingOAuth = await _context.UserOAuths
                .FirstOrDefaultAsync(o => o.ProviderName == providerName && o.ProviderKey == providerKey);

            if (existingOAuth != null)
            {
                // Already linked to another account
                return false;
            }

            // Create the link
            var newOAuth = new UserOAuth
            {
                UserId = userId,
                ProviderName = providerName,
                ProviderKey = providerKey
            };

            _context.UserOAuths.Add(newOAuth);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}