using Microsoft.Extensions.Logging;

namespace Modules.Common.Application.Logging;

/// <summary>
/// Every log event emitted by the shared application layer.
/// </summary>
/// <remarks>
/// See <c>docs/log-event-ids.md</c> for the range this catalogue owns and how ids are
/// allocated across the solution.
/// </remarks>
public static partial class CommonApplicationLogs
{
    /// <summary>The first event id reserved for this catalogue.</summary>
    public const int EventIdRangeStart = 2_000;

    /// <summary>The last event id reserved for this catalogue.</summary>
    public const int EventIdRangeEnd = 2_999;

    /// <summary>
    /// A request failed validation before reaching its handler.
    /// </summary>
    /// <remarks>
    /// One event covers every use case rather than one per validator. The use case is a
    /// structured field (<c>ContextMessage</c>), so "show me every failed registration"
    /// stays a query and the id does not have to be re-allocated for each new slice.
    /// </remarks>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="contextMessage">What was being validated, e.g. "Register user".</param>
    /// <param name="validationErrors">The failures, comma separated.</param>
    [LoggerMessage(
        EventId = 2_000,
        Level = LogLevel.Warning,
        Message = "{ContextMessage}: {ValidationErrors}")]
    public static partial void ValidationFailed(
        this ILogger logger,
        string contextMessage,
        string validationErrors);
}
