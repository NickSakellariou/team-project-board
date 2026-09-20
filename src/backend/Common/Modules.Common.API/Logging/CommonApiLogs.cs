using Microsoft.Extensions.Logging;

namespace Modules.Common.API.Logging;

/// <summary>
/// Every log event emitted by the shared API layer.
/// </summary>
/// <remarks>
/// <para>
/// Lives in Common for the same reason <see cref="Modules.Common.Domain.Errors.RequestErrors"/>
/// does: these events are about the request pipeline itself, and read identically whichever
/// module the request was heading for. Anything that names a concept a module owns belongs
/// in that module's own catalogue.
/// </para>
/// <para>
/// See <c>docs/log-event-ids.md</c> for the range this catalogue owns and how ids are
/// allocated across the solution.
/// </para>
/// </remarks>
public static partial class CommonApiLogs
{
    /// <summary>The first event id reserved for this catalogue.</summary>
    public const int EventIdRangeStart = 1_000;

    /// <summary>The last event id reserved for this catalogue.</summary>
    public const int EventIdRangeEnd = 1_999;

    /// <summary>
    /// An exception escaped a request and was turned into a 500.
    /// </summary>
    /// <remarks>
    /// Expected failures never reach here — they come back as a failed <c>Result&lt;T&gt;</c>
    /// and become a 4xx. Anything carrying this id is a bug or infrastructure being down,
    /// which makes it the natural thing to alert on.
    /// </remarks>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="exception">The exception that escaped.</param>
    /// <param name="method">The HTTP method of the failed request.</param>
    /// <param name="path">The path of the failed request.</param>
    [LoggerMessage(
        EventId = 1_000,
        Level = LogLevel.Error,
        Message = "Unhandled exception while processing {Method} {Path}")]
    public static partial void UnhandledException(
        this ILogger logger,
        Exception exception,
        string method,
        string path);
}
