using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ACA.Infrastructure.Identity;

public static class IdentitySeeder
{
    public static readonly string[] Roles = { "Admin", "Moderator", "Member" };

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, CancellationToken ct = default)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");

        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                logger.LogInformation("Created role {Role}", role);
            }
        }

        var email = configuration["AdminSeed:Email"];
        var password = configuration["AdminSeed:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("AdminSeed credentials not configured; skipping admin creation.");
            return;
        }

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is null)
        {
            var admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await userManager.CreateAsync(admin, password);
            if (!result.Succeeded)
            {
                logger.LogWarning("Could not create seed admin: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }

            existing = admin;
            logger.LogInformation("Created seed admin {Email}", email);
        }

        if (!await userManager.IsInRoleAsync(existing, "Admin"))
        {
            await userManager.AddToRoleAsync(existing, "Admin");
        }
    }
}
