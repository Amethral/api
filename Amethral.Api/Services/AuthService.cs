using Microsoft.EntityFrameworkCore;
using Amethral.Api.Data;
using Amethral.Api.Data.Entities;
using Amethral.Common.DTOs;

namespace Amethral.Api.Services
{
    public class AuthService
    {
        private readonly AppDbContext _context;

        public AuthService(AppDbContext context)
        {
            _context = context;
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
                AuthUrl = $"https://monsiteweb.com/login?token={token}", // URL Frontend
                ExpiresAt = entity.ExpiresAt
            };
        }

        // 2. Register Email + Validation du Ticket
        public async Task<bool> RegisterWithEmailAsync(RegisterRequest request)
        {
            // Vérification doublon email... (simplifié ici)
            
            var user = new User
            {
                Username = request.Username,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // LIEN CRITIQUE : On attache le User au Token
            return await LinkUserToToken(request.WebToken, user.Id);
        }

        // 3. Login Email + Validation du Ticket
        public async Task<bool> LoginWithEmailAsync(LoginRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return false;

            return await LinkUserToToken(request.WebToken, user.Id);
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
                .Include(t => t.UserId) // Pseudo include, on veut juste l'ID
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
    }
}