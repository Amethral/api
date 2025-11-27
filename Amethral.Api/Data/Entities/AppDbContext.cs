using Microsoft.EntityFrameworkCore;
using Amethral.Api.Data.Entities;

namespace Amethral.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<UserOAuth> UserOAuths { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Index unique pour éviter qu'un compte Google soit lié 2 fois
            modelBuilder.Entity<UserOAuth>()
                .HasIndex(o => new { o.ProviderName, o.ProviderKey })
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();
        }
    }
}