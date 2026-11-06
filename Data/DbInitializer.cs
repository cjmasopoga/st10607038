using Microsoft.EntityFrameworkCore;
using SmartMonitoringSolutions.Models;

namespace SmartMonitoringSolutions.Data;

/// <summary>Applies pending EF Core migrations and seeds a default admin/farmer/agronomist account on first run.</summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<Services.PasswordHasherService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        try
        {
            await context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database migration failed or database is unreachable at startup. The app will continue to start.");
            return;
        }

        if (!await context.Users.AnyAsync())
        {
            var seedUsers = new (string Username, string Password, UserRole Role)[]
            {
                ("admin", "ChangeMe123!", UserRole.Admin),
                ("agronomist1", "ChangeMe123!", UserRole.Agronomist),
                ("farmer1", "ChangeMe123!", UserRole.Farmer),
            };

            foreach (var (username, password, role) in seedUsers)
            {
                var user = new User { Username = username, Role = role };
                user.PasswordHash = hasher.Hash(user, password);
                context.Users.Add(user);
            }

            await context.SaveChangesAsync();
            logger.LogInformation("Seeded default user accounts (admin/agronomist1/farmer1). Change these passwords immediately.");
        }
    }
}
