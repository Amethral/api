using System.ComponentModel.DataAnnotations;

namespace Amethral.Api.Data.Entities
{
    public class User
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PasswordHash { get; set; } // Nullable si compte 100% Google
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Relations
        public ICollection<UserOAuth> OAuths { get; set; } = new List<UserOAuth>();

    }
}