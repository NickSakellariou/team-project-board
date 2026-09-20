using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Common.Infrastructure.Database;
using Modules.Common.Infrastructure.Policies;
using Modules.Users.Domain.Authentication;
using Modules.Users.Domain.Users;
using Modules.Users.Infrastructure.Authorization;
using Modules.Users.Infrastructure.Database;
using Modules.Users.Infrastructure.Policies;

namespace Modules.Users.Infrastructure;

/// <summary>
/// Registers the Users module's infrastructure: database, Identity and authentication.
/// </summary>
public static class UsersInfrastructureDependencyInjection
{
    /// <summary>The connection string name, declared by the Aspire AppHost.</summary>
    private const string ConnectionStringName = "teamprojectboard";

    /// <summary>
    /// Adds the Users DbContext, ASP.NET Core Identity and the authentication service.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">Configuration holding the connection string.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddUsersInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddUsersDatabase(configuration);
        services.AddUsersIdentity();

        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IPolicyFactory, UsersPolicyFactory>();
        services.AddScoped<IModuleDatabaseMigrator, UsersDatabaseMigrator>();

        return services;
    }

    private static void AddUsersDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' was not found. " +
                "Run the application through TeamProjectBoard.AppHost, which injects it.");

        // Why singleton for the interceptor while the DbContext is scoped: the interceptor
        // is stateless, so one instance can serve every request. Injecting a scoped service
        // into a singleton would be the reverse and a bug (a "captive dependency"); this
        // direction is fine.
        services.AddSingleton<AuditableInterceptor>();

        services.AddDbContext<UsersDbContext>((provider, options) =>
        {
            options
                .UseNpgsql(connectionString, npgsql =>
                    // Each module records its migrations in its own schema. Without this
                    // every module would share one __EFMigrationsHistory table in public
                    // and each would see the others' migrations as unknown.
                    npgsql.MigrationsHistoryTable(DbConsts.MigrationHistoryTable, DbConsts.Schema))
                .AddInterceptors(provider.GetRequiredService<AuditableInterceptor>())
                // Why: EF's default would produce "NormalizedEmail" and "CreatedAtUtc" as
                // column names, which in Postgres must then be double-quoted in every
                // hand-written query. snake_case is the Postgres convention, so the
                // database stays pleasant to use outside of EF.
                .UseSnakeCaseNamingConvention();
        });
    }

    private static void AddUsersIdentity(this IServiceCollection services)
    {
        // Why AddIdentityCore and not AddIdentity: AddIdentity also wires up cookie
        // authentication and its associated redirect behaviour — an unauthenticated
        // request gets a 302 to /Account/Login instead of a 401. That is right for a
        // server-rendered app and wrong for an API consumed by React, where the JWT
        // scheme configured in AddCoreInfrastructure must stay the default.
        services
            .AddIdentityCore<User>(options =>
            {
                // Identity enforces these on registration and password change. They are
                // deliberately modest: length does far more for password strength than
                // character-class rules, which mostly push people towards "Password1!".
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;

                // Two accounts cannot share an email; it is the login identifier.
                options.User.RequireUniqueEmail = true;

                // Lockout blunts online password guessing: after five failures the account
                // rejects attempts for five minutes, whether or not the password is right.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<Role>()
            .AddEntityFrameworkStores<UsersDbContext>()
            // Supplies the token generators Identity uses for password reset and email
            // confirmation. Unrelated to our JWTs, despite the name.
            .AddDefaultTokenProviders();
    }
}
