namespace Modules.Common.Domain.Results;

/// <summary>
/// Marks a successful operation that has no value to return.
/// </summary>
/// <remarks>
/// Used as <c>Result&lt;Success&gt;</c> for commands such as "delete user", where the
/// interesting information is only whether it worked. Being an empty struct, it costs
/// nothing at run time.
/// </remarks>
public readonly record struct Success;
