using Modules.Common.API.Abstractions;
using Modules.Common.Domain.Handlers;
using NetArchTest.Rules;

namespace Modules.Common.Tests.Architecture;

/// <summary>
/// Asserts the naming and visibility conventions that the startup assembly scans rely on.
/// </summary>
/// <remarks>
/// Endpoints, handlers and validators are wired up by reflection, so a class that does not
/// follow the convention is not a compile error — it is an endpoint that silently does not
/// exist, discovered when someone calls it. These tests turn that into a build failure.
/// </remarks>
public class ConventionTests
{
    /// <summary>Endpoint classes must be named <c>*Endpoint</c>.</summary>
    [Fact]
    public void ApiEndpoints_ShouldBeNamed_Endpoint()
    {
        var result = Types.InAssembly(ModuleAssemblies.UsersFeatures)
            .That()
            .ImplementInterface(typeof(IApiEndpoint))
            .Should()
            .HaveNameEndingWith("Endpoint", StringComparison.Ordinal)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure(result));
    }

    /// <summary>Handler classes must be named <c>*Handler</c>.</summary>
    [Fact]
    public void Handlers_ShouldBeNamed_Handler()
    {
        var result = Types.InAssembly(ModuleAssemblies.UsersFeatures)
            .That()
            .ImplementInterface(typeof(IHandler))
            .And()
            .AreClasses()
            .Should()
            .HaveNameEndingWith("Handler", StringComparison.Ordinal)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure(result));
    }

    /// <summary>
    /// Handler implementations must be sealed.
    /// </summary>
    /// <remarks>
    /// A handler is a leaf: nothing derives from it, and inheritance between use cases
    /// would be a design mistake. Sealing states that and lets the JIT devirtualize its
    /// calls for free.
    /// </remarks>
    [Fact]
    public void Handlers_ShouldBeSealed()
    {
        var result = Types.InAssembly(ModuleAssemblies.UsersFeatures)
            .That()
            .ImplementInterface(typeof(IHandler))
            .And()
            .AreClasses()
            .Should()
            .BeSealed()
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure(result));
    }

    /// <summary>
    /// Handler implementations must not be public.
    /// </summary>
    /// <remarks>
    /// A handler is an internal detail of its module, reached through its own interface.
    /// Making one public invites another module to call it directly and bypass the
    /// PublicApi boundary entirely.
    /// </remarks>
    [Fact]
    public void Handlers_ShouldNotBePublic()
    {
        var result = Types.InAssembly(ModuleAssemblies.UsersFeatures)
            .That()
            .ImplementInterface(typeof(IHandler))
            .And()
            .AreClasses()
            .Should()
            .NotBePublic()
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure(result));
    }

    /// <summary>
    /// Every slice with an endpoint must have a handler.
    /// </summary>
    /// <remarks>
    /// Catches the half-finished slice: an endpoint whose handler was never written, or
    /// whose handler was renamed out of the convention and so is no longer registered.
    /// </remarks>
    [Fact]
    public void EveryEndpoint_ShouldHave_AMatchingHandler()
    {
        var endpointSlices = Types.InAssembly(ModuleAssemblies.UsersFeatures)
            .That()
            .ImplementInterface(typeof(IApiEndpoint))
            .GetTypes()
            .Select(type => type.Name.Replace("Endpoint", string.Empty, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        var handlerSlices = Types.InAssembly(ModuleAssemblies.UsersFeatures)
            .That()
            .ImplementInterface(typeof(IHandler))
            .And()
            .AreClasses()
            .GetTypes()
            .Select(type => type.Name.Replace("Handler", string.Empty, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        var endpointsWithoutHandlers = endpointSlices.Except(handlerSlices, StringComparer.Ordinal).ToList();

        Assert.True(
            endpointsWithoutHandlers.Count == 0,
            $"These endpoints have no matching handler: {string.Join(", ", endpointsWithoutHandlers)}");
    }

    private static string FormatFailure(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}";
}
