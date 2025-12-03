using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace Amethral.Api.Data
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var builder = new DbContextOptionsBuilder<AppDbContext>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            // Support for postgres:// URL format (common in cloud providers like Coolify, Railway, Heroku)
            if (!string.IsNullOrEmpty(connectionString) && 
                (connectionString.StartsWith("postgres://") || connectionString.StartsWith("postgresql://")))
            {
                try 
                {
                    var uri = new Uri(connectionString);
                    var userInfo = uri.UserInfo.Split(':');
                    var username = Uri.UnescapeDataString(userInfo[0]);
                    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
                    
                    connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={username};Password={password}";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing DATABASE_URL: {ex.Message}");
                    // Fallback to original string if parsing fails
                }
            }

            builder.UseNpgsql(connectionString);

            return new AppDbContext(builder.Options);
        }
    }
}
