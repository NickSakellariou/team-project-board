using Microsoft.Extensions.Logging;

namespace Modules.Common.Infrastructure.Logging;

/// <summary>
/// Every log event emitted by the shared infrastructure layer.
/// </summary>
/// <remarks>
/// See <c>docs/log-event-ids.md</c> for the range this catalogue owns and how ids are
/// allocated across the solution.
/// </remarks>
public static partial class CommonInfrastructureLogs
{
    /// <summary>The first event id reserved for this catalogue.</summary>
    public const int EventIdRangeStart = 3_000;

    /// <summary>The last event id reserved for this catalogue.</summary>
    public const int EventIdRangeEnd = 3_999;

    /// <summary>
    /// A module's authorization policies were added to the application's options.
    /// </summary>
    /// <remarks>
    /// Startup-only, and the cheapest answer to "why is this endpoint returning 403 in the
    /// deployed environment?" — a module whose policies never registered is visible here
    /// and nowhere else.
    /// </remarks>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="policyCount">How many policies the module contributed.</param>
    /// <param name="moduleName">The module that contributed them.</param>
    [LoggerMessage(
        EventId = 3_000,
        Level = LogLevel.Information,
        Message = "Registered {PolicyCount} authorization policies for module {ModuleName}")]
    public static partial void AuthorizationPoliciesRegistered(
        this ILogger logger,
        int policyCount,
        string moduleName);
}
