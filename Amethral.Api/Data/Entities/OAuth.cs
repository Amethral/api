using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Amethral.Api.Data.Entities
{
    public class UserOAuth
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        public string ProviderName { get; set; } = string.Empty; // "Google", "Discord"
        public string ProviderKey { get; set; } = string.Empty;  // L'ID unique chez Google
    }
}