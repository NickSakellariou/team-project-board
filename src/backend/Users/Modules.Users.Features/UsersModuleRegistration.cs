using FluentValidation;
using Microsoft.Extensions.Configuration;
using Modules.Common.Application.Extensions;
using Modules.Users.Features.InternalApi;
using Modules.Users.PublicApi;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// The Users module's single registration entry point.
/// </summary>
/// <remarks>
/// <para>
/// One method per module, called once from <c>Program.cs</c>. The host knows only
/// <c>AddUsersModule</c> — never <c>UsersDbContext</c>, never a handler, never a policy
/// name. A module can restructure itself entirely without the host changing.
/// </para>
/// <para>
/// Three of the four registrations below are assembly scans, so adding a slice needs no
/// edit here either.
/// </para>
/// </remarks>
public static class UsersModuleRegistration
{
    /// <summary>
    /// Registers everything the Users module needs.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">Configuration holding the connection string and auth settings.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddUsersModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddUsersInfrastructure(configuration);

        var marker = typeof(UsersModuleRegistration);

        services.RegisterApiEndpointsFromAssemblyContaining(marker);
        services.RegisterHandlersFromAssemblyContaining(marker);
        services.AddValidatorsFromAssembly(marker.Assembly, includeInternalTypes: true);

        // The module's front door for other modules. Scoped, because it depends on the
        // scoped UsersDbContext.
        services.AddScoped<IUserModuleApi, UserModuleApi>();

        return services;
    }
}
