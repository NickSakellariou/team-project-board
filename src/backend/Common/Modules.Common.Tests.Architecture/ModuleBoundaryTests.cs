using NetArchTest.Rules;

namespace Modules.Common.Tests.Architecture;

/// <summary>
/// Asserts that the module boundaries described in docs/adr/ actually hold.
/// </summary>
/// <remarks>
/// <para>
/// Architecture erodes one reasonable-looking shortcut at a time. Nothing stops a
/// developer in a hurry from adding a ProjectReference and calling into another module
/// directly — nothing except a test that fails when they do.
/// </para>
/// <para>
/// These run in milliseconds and need no database. They are the cheapest tests in the
/// solution and, for a modular monolith, among the most valuable: they are what makes the
/// word "modular" true rather than aspirational.
/// </para>
/// </remarks>
public class ModuleBoundaryTests
{
    private const string UsersNamespace = "Modules.Users";
    private const string UsersPublicApiNamespace = "Modules.Users.PublicApi";

    /// <summary>
    /// The Domain layer must not depend on infrastructure, features or the web.
    /// </summary>
    /// <remarks>
    /// The dependency rule of Clean Architecture, stated as a test. If the domain ever
    /// references EF Core or ASP.NET Core, the business rules can no longer be tested or
    /// reasoned about without those frameworks.
    ///
    /// Note the deliberate exception: Microsoft.AspNetCore.Identity is permitted, because
    /// User derives from IdentityUser. That trade is recorded in
    /// docs/adr/0008-identity-and-jwt.md — an accepted exception, not an oversight.
    /// </remarks>
    [Fact]
    public void UsersDomain_ShouldNotDependOn_InfrastructureOrFeatures()
    {
        var result = Types.InAssembly(ModuleAssemblies.UsersDomain)
            .Should()
            .NotHaveDependencyOnAny(
                "Modules.Users.Infrastructure",
                "Modules.Users.Features",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore.Http",
                "Microsoft.AspNetCore.Builder")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure(result));
    }

    /// <summary>
    /// The PublicApi project must depend on nothing inside its own module.
    /// </summary>
    /// <remarks>
    /// This is the load-bearing test of the whole scheme. Every other module is allowed to
    /// reference PublicApi, so anything reachable from it is transitively reachable by all
    /// of them. The moment a contract exposes a domain entity, the boundary is gone —
    /// and it would go quietly, because everything would still compile.
    /// </remarks>
    [Fact]
    public void UsersPublicApi_ShouldNotDependOn_TheRestOfItsModule()
    {
        var result = Types.InAssembly(ModuleAssemblies.UsersPublicApi)
            .Should()
            .NotHaveDependencyOnAny(
                "Modules.Users.Domain",
                "Modules.Users.Infrastructure",
                "Modules.Users.Features",
                "Modules.Common")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure(result));
    }

    /// <summary>
    /// A module may only reach another module through its PublicApi.
    /// </summary>
    /// <remarks>
    /// With one module this passes trivially, and that is fine — it is written now so it
    /// is already in place when Projects, Boards and Collaboration arrive, which is
    /// exactly when the rule starts being tempting to break.
    /// </remarks>
    [Fact]
    public void UsersModule_ShouldNotDependOn_OtherModulesInternals()
    {
        string[] otherModuleInternals =
        [
            "Modules.Projects.Domain", "Modules.Projects.Infrastructure", "Modules.Projects.Features",
            "Modules.Boards.Domain", "Modules.Boards.Infrastructure", "Modules.Boards.Features",
            "Modules.Collaboration.Domain", "Modules.Collaboration.Infrastructure", "Modules.Collaboration.Features"
        ];

        var result = Types.InAssemblies(ModuleAssemblies.UsersModule)
            .Should()
            .NotHaveDependencyOnAny(otherModuleInternals)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure(result));
    }

    /// <summary>
    /// Nothing outside the Users module may reference its internals.
    /// </summary>
    [Fact]
    public void CommonProjects_ShouldNotDependOn_AnyModule()
    {
        var result = Types.InAssembly(ModuleAssemblies.CommonDomain)
            .Should()
            .NotHaveDependencyOn(UsersNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure(result));
    }

    /// <summary>
    /// The PublicApi contracts must be public, or no other module could use them.
    /// </summary>
    [Fact]
    public void UsersPublicApi_Types_ShouldBePublic()
    {
        var result = Types.InAssembly(ModuleAssemblies.UsersPublicApi)
            .That()
            .DoNotHaveNameEndingWith("AssemblyReference", StringComparison.Ordinal)
            .Should()
            .BePublic()
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure(result));
    }

    /// <summary>
    /// Confirms the PublicApi namespace exists and holds the module's contract.
    /// </summary>
    /// <remarks>
    /// A guard against the previous tests passing for the wrong reason: an empty or
    /// deleted PublicApi project would satisfy every "must not depend on" rule above.
    /// </remarks>
    [Fact]
    public void UsersPublicApi_ShouldExposeAtLeastOneContract()
    {
        var contractTypes = Types.InAssembly(ModuleAssemblies.UsersPublicApi)
            .That()
            .ResideInNamespaceStartingWith(UsersPublicApiNamespace)
            .And()
            .AreInterfaces()
            .GetTypes();

        Assert.NotEmpty(contractTypes);
    }

    private static string FormatFailure(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}";
}
