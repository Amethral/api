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

        public string GenerateWebJwt(User user)
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