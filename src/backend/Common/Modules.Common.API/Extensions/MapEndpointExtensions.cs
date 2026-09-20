using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Modules.Common.API.Abstractions;

namespace Modules.Common.API.Extensions;

/// <summary>
/// Discovers and maps <see cref="IApiEndpoint"/> implementations.
/// </summary>
public static class MapEndpointExtensions
{
    /// <summary>
    /// Registers every <see cref="IApiEndpoint"/> in the assembly containing
    /// <paramref name="marker"/>.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="marker">Any type from the assembly to scan.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection RegisterApiEndpointsFromAssemblyContaining(
        this IServiceCollection services,
        Type marker)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(marker);

        var endpointTypes = marker.Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                           && type.IsAssignableTo(typeof(IApiEndpoint)));

        var serviceDescriptors = endpointTypes
            .Select(type => ServiceDescriptor.Transient(typeof(IApiEndpoint), type))
            .ToArray();

        // Why TryAddEnumerable rather than Add: every endpoint is registered under the
        // same service type (IApiEndpoint), and TryAddEnumerable skips a descriptor whose
        // implementation type is already present. Plain Add would register a duplicate if
        // a module's registration ran twice, and the endpoint's route would be mapped
        // twice — which ASP.NET Core rejects at startup with an ambiguous-route error.
        services.TryAddEnumerable(serviceDescriptors);

        return services;
    }

    /// <summary>
    /// Maps the route of every registered <see cref="IApiEndpoint"/>.
    /// </summary>
    /// <param name="app">The application to map routes onto.</param>
    /// <returns>The same application, for chaining.</returns>
    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var endpoints = app.Services.GetRequiredService<IEnumerable<IApiEndpoint>>();

        foreach (var endpoint in endpoints)
        {
            endpoint.MapEndpoint(app);
        }

        return app;
    }
}
