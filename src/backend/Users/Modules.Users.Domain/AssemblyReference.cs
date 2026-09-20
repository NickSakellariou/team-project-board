using System.Reflection;

namespace Modules.Users.Domain;

/// <summary>
/// A handle on this project's assembly.
/// </summary>
/// <remarks>
/// Exists so other projects — chiefly the architecture tests — can name this assembly in
/// a way the compiler checks. The alternative,
/// <c>Assembly.Load("Modules.Users.Domain")</c>, is a string that silently returns nothing
/// after a rename, quietly turning an architecture test into one that passes because it
/// examines an empty set.
/// </remarks>
public static class AssemblyReference
{
    /// <summary>Gets the assembly containing the Users domain.</summary>
    public static Assembly Assembly { get; } = typeof(AssemblyReference).Assembly;
}
