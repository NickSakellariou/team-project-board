using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Modules.Users.Domain.Policies;
using Modules.Users.Domain.Users;

namespace TeamProjectBoard.Host.Seeding;

/// <summary>
/// Creates the roles, their permissions and a development admin account.
/// </summary>
/// <remarks>
/// <para>
/// Roles are not optional data. Registration assigns <see cref="SystemRoles.User"/>, so
/// without seeding, the very first registration fails — and the <c>users:*</c> policies
/// would reject everyone, because no role carries the claims they require.
/// </para>
/// <para>
/// Every step is written to be safe to run repeatedly, since this executes on every
/// startup in Development.
/// </para>
/// </remarks>
internal sealed class UserSeedService(
    RoleManager<Role> roleManager,
    UserManager<User> userManager,
    IConfiguration configuration,
    ILogger<UserSeedService> logger)
{
    public async Task SeedAsync()
    {
        await SeedRolesAsync();
        await SeedAdminPermissionsAsync();
        await SeedDevelopmentAdminAsync();
    }

    private async Task SeedRolesAsync()
    {
        foreach (var roleName in SystemRoles.All)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            await roleManager.CreateAsync(new Role { Name = roleName });
            logger.LogInformation("Created role {RoleName}", roleName);
        }
    }

    // This is where "Admin can manage users" is actually decided. The endpoints require
    // claims such as users:delete; attaching those claims to the Admin role is what makes
    // an admin able to satisfy them. Change this method and the permission model changes,
    // with no endpoint touched.
    private async Task SeedAdminPermissionsAsync()
    {
        var adminRole = await roleManager.FindByNameAsync(SystemRoles.Admin);
        if (adminRole is null)
        {
            return;
        }

        var existingClaims = await roleManager.GetClaimsAsync(adminRole);

        foreach (var permission in UserPolicyConsts.All)
        {
            // The claim's Type is the permission name and its Value is "true" — the
            // policies use RequireClaim(name), which checks only that a claim of that type
            // is present, so the value is a placeholder.
            if (existingClaims.Any(claim => string.Equals(claim.Type, permission, StringComparison.Ordinal)))
            {
                continue;
            }

            await roleManager.AddClaimAsync(adminRole, new Claim(permission, "true"));
            logger.LogInformation("Granted {Permission} to the {RoleName} role", permission, SystemRoles.Admin);
        }
    }

    // No CancellationToken: none of the UserManager or RoleManager methods used here
    // accept one, so taking a token would imply a cancellation this method cannot honour.
    private async Task SeedDevelopmentAdminAsync()
    {
        var email = configuration["Seed:AdminEmail"];
        var password = configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("No seed admin configured; skipping.");
            return;
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var admin = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = email,
            UserName = email,
            DisplayName = "Development Admin",
            // Only reasonable because this account exists solely in Development. A real
            // admin account would go through the normal confirmation flow.
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(admin, password);
        if (!createResult.Succeeded)
        {
            logger.LogError(
                "Could not seed the admin account: {Errors}",
                string.Join(", ", createResult.Errors.Select(error => error.Description)));

            return;
        }

        await userManager.AddToRoleAsync(admin, SystemRoles.Admin);

        logger.LogWarning(
            "Seeded development admin {Email}. This account exists only in Development.",
            email);
    }
}
