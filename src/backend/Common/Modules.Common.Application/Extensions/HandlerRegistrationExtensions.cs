using Microsoft.Extensions.DependencyInjection;
using Modules.Common.Domain.Handlers;

namespace Modules.Common.Application.Extensions;

/// <summary>
/// Registers a module's use-case handlers in the DI container by scanning its assembly.
/// </summary>
public static class HandlerRegistrationExtensions
{
    /// <summary>
    /// Finds every <see cref="IHandler"/> implementation in the assembly containing
    /// <paramref name="marker"/> and registers it against its own handler interface.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="marker">Any type from the assembly to scan.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This is the alternative to writing
    /// <c>services.AddScoped&lt;IRegisterUserHandler, RegisterUserHandler&gt;();</c> once
    /// per slice. Adding a new slice then requires no DI edit at all — a whole class of
    /// "works on my machine until you hit that one endpoint" bug disappears.
    /// </para>
    /// <para>
    /// The trade for that convenience is that the wiring is no longer greppable: nothing
    /// in the source says <c>RegisterUserHandler</c> is registered. The architecture tests
    /// enforce the naming convention this scan relies on, which is what keeps it honest.
    /// </para>
    /// <para>
    /// Handlers are registered <b>scoped</b> — one instance per HTTP request — because
    /// they depend on a <c>DbContext</c>, which is itself scoped. See
    /// docs/concepts/dependency-injection.md.
    /// </para>
    /// </remarks>
    public static IServiceCollection RegisterHandlersFromAssemblyContaining(
        this IServiceCollection services,
        Type marker)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(marker);

        var handlerTypes = marker.Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && type.IsAssignableTo(typeof(IHandler)));

        foreach (var implementationType in handlerTypes)
        {
            // A handler implements exactly one narrow interface (IRegisterUserHandler),
            // which in turn derives from IHandler. Register against that narrow interface,
            // not IHandler itself, so consumers depend on one use case rather than all of them.
            var serviceType = implementationType.GetInterfaces()
                .FirstOrDefault(i => i != typeof(IHandler) && i.IsAssignableTo(typeof(IHandler)));

            if (serviceType is not null)
            {
                services.AddScoped(serviceType, implementationType);
            }
        }

        return services;
    }
}
