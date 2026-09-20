using System.Reflection;

namespace Modules.Common.Tests.Architecture;

/// <summary>
/// The assemblies under test, resolved through each project's AssemblyReference so a
/// rename is a compile error rather than a silently empty test.
/// </summary>
internal static class ModuleAssemblies
{
    internal static readonly Assembly CommonDomain = typeof(Common.Domain.Results.Error).Assembly;

    internal static readonly Assembly UsersDomain = Users.Domain.AssemblyReference.Assembly;
    internal static readonly Assembly UsersFeatures = Users.Features.AssemblyReference.Assembly;
    internal static readonly Assembly UsersInfrastructure = Users.Infrastructure.AssemblyReference.Assembly;
    internal static readonly Assembly UsersPublicApi = Users.PublicApi.AssemblyReference.Assembly;

    /// <summary>Every assembly belonging to the Users module.</summary>
    internal static Assembly[] UsersModule =>
        [UsersDomain, UsersFeatures, UsersInfrastructure, UsersPublicApi];
}
