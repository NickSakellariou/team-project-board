using Microsoft.AspNetCore.Builder;

namespace Modules.Common.API.Abstractions;

/// <summary>
/// One HTTP endpoint, able to register its own route.
/// </summary>
/// <remarks>
/// <para>
/// This is what replaces controllers. A controller is a class that accumulates endpoints
/// until it is a thousand lines long and every action shares constructor dependencies it
/// mostly does not need. Here each endpoint is its own class, in its own feature folder,
/// next to the handler and validator it belongs with.
/// </para>
/// <para>
/// At startup <c>RegisterApiEndpointsFromAssemblyContaining</c> finds every implementation
/// and <c>MapApiEndpoints</c> calls <see cref="MapEndpoint"/> on each, so adding a route
/// means adding a file — nothing central needs editing.
/// </para>
/// </remarks>
public interface IApiEndpoint
{
    /// <summary>
    /// Registers this endpoint's route on the application.
    /// </summary>
    /// <param name="app">The application to map the route onto.</param>
    void MapEndpoint(WebApplication app);
}
