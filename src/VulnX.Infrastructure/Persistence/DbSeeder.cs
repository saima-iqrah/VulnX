using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VulnX.Domain.Common;
using VulnX.Infrastructure.Identity;

namespace VulnX.Infrastructure.Persistence;

public static class DbSeeder
{
    /// Seeds roles, and an admin account only if Seed:AdminEmail and Seed:AdminPassword are provided
    /// through configuration/secrets. No credentials are hardcoded.
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in new[] { Roles.User, Roles.Admin })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new ApplicationRole(role));
                if (!result.Succeeded)
                    throw new InvalidOperationException($"Could not create role {role}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }

        if (!bool.TryParse(config["Seed:Enabled"], out var seedAdmin) || !seedAdmin)
        {
            logger.LogInformation("Admin seed disabled (Seed:Enabled is not true). Roles are ensured.");
            return;
        }

        var email = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("Admin seed skipped (Seed:AdminEmail / Seed:AdminPassword not set).");
            return;
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            logger.LogInformation("Admin seed skipped (user already exists).");
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = "Administrator"
        };
        var create = await userManager.CreateAsync(admin, password);
        if (!create.Succeeded)
            throw new InvalidOperationException($"Admin seed failed: {string.Join(", ", create.Errors.Select(e => e.Description))}");

        await userManager.AddToRoleAsync(admin, Roles.Admin);
        logger.LogInformation("Admin account created.");
    }
}
