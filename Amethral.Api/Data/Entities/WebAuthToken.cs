using System.ComponentModel.DataAnnotations;

namespace Amethral.Api.Data.Entities
{
    public class WebAuthToken
    {
        [Key]
        public string Token { get; set; } = string.Empty; // Random String
        public string DeviceId { get; set; } = string.Empty;
        
        public Guid? UserId { get; set; } // C'est LE champ clé. Null au début, rempli après login.
        
        public bool IsConsumed { get; set; } = false;
        public DateTime ExpiresAt { get; set; }
    }
}