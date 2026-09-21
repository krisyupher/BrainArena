using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;
using BrainArena.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BrainArena.Infrastructure.Auth;

public static class AdminSeeder
{
    /// <summary>
    /// Ensures the admin account named in config exists and has the Admin role.
    /// No-op if Admin:Email/Admin:Password aren't configured.
    /// </summary>
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var email = config["Admin:Email"]?.Trim().ToLowerInvariant();
        var password = config["Admin:Password"];
        var displayName = config["Admin:DisplayName"] ?? "Admin";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        var db = scope.ServiceProvider.GetRequiredService<BrainArenaDbContext>();
        var hasher = new PasswordHasher();

        var admin = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (admin is null)
        {
            admin = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                DisplayName = displayName,
                PasswordHash = string.Empty,
                Role = UserRole.Admin,
                CreatedAt = DateTimeOffset.UtcNow
            };
            admin.PasswordHash = hasher.Hash(admin, password);
            db.Users.Add(admin);
        }
        else if (admin.Role != UserRole.Admin)
        {
            admin.Role = UserRole.Admin;
        }

        await db.SaveChangesAsync();
    }
}
