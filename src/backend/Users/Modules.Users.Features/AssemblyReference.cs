using System.Reflection;

namespace Modules.Users.Features;

/// <summary>
/// A handle on this project's assembly, for the architecture tests.
/// </summary>
public static class AssemblyReference
{
    /// <summary>Gets the assembly containing the Users features.</summary>
    public static Assembly Assembly { get; } = typeof(AssemblyReference).Assembly;
}
