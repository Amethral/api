using Microsoft.EntityFrameworkCore;
using Amethral.Api.Data.Entities;

namespace Amethral.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<UserOAuth> UserOAuths { get; set; }
        public DbSet<WebAuthToken> WebAuthTokens { get; set; }
        public DbSet<GameSession> GameSessions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Index unique pour éviter qu'un compte Google soit lié 2 fois
            modelBuilder.Entity<UserOAuth>()
                .HasIndex(o => new { o.ProviderName, o.ProviderKey })
                .IsUnique();

            // Index pour le polling rapide
            modelBuilder.Entity<WebAuthToken>()
                .HasIndex(t => t.Token);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();
        }
    }
}