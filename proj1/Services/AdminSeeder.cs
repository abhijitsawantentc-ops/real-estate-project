using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using proj1.Data;
using proj1.Models;

namespace proj1.Services;

public static class AdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Profile>>();

        // First, create default admin (Admin/Admin) if it doesn't exist
        const string defaultAdminEmail = "admin@haven.local";
        const string defaultAdminPassword = "Admin";
        var defaultAdmin = await db.Profiles.SingleOrDefaultAsync(x => x.Email == defaultAdminEmail);
        if (defaultAdmin is null)
        {
            defaultAdmin = new Profile
            {
                Email = defaultAdminEmail,
                FullName = "Admin",
                Role = PlatformRoles.Admin,
                IsActive = true
            };
            defaultAdmin.PasswordHash = hasher.HashPassword(defaultAdmin, defaultAdminPassword);
            db.Profiles.Add(defaultAdmin);
            await db.SaveChangesAsync();
            logger.LogInformation("Default platform administrator created (Email: admin@haven.local, Password: Admin).");
        }

        // Then, create configured admin from environment if provided
        var email = configuration["Platform:AdminEmail"]?.Trim().ToLowerInvariant();
        var password = configuration["Platform:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("Additional admin bootstrap skipped; configure Platform__AdminEmail and Platform__AdminPassword to create another admin account.");
            return;
        }

        var profile = await db.Profiles.SingleOrDefaultAsync(x => x.Email == email);
        if (profile is null)
        {
            profile = new Profile
            {
                Email = email,
                FullName = "Platform Administrator",
                Role = PlatformRoles.Admin,
                IsActive = true
            };
            profile.PasswordHash = hasher.HashPassword(profile, password);
            db.Profiles.Add(profile);
            await db.SaveChangesAsync();
            logger.LogInformation("Configured platform administrator created.");
        }
        else if (profile.Role != PlatformRoles.Admin)
        {
            throw new InvalidOperationException("The configured admin email is already assigned to a non-admin profile.");
        }
    }
}
