using System.Reflection;

namespace Modules.Users.PublicApi;

/// <summary>
/// A handle on this project's assembly, for the architecture tests.
/// </summary>
public static class AssemblyReference
{
    /// <summary>Gets the assembly containing the Users module's public contracts.</summary>
    public static Assembly Assembly { get; } = typeof(AssemblyReference).Assembly;
}
