using System.ComponentModel.DataAnnotations;

namespace Amethral.Api.Data.Entities
{
    public class GameSession
    {
        [Key]
        public string SessionToken { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
    }
}