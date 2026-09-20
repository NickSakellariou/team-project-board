using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Modules.Common.API.Logging;

namespace Modules.Common.Tests.Unit.Logging;

/// <summary>
/// Pins the id, name, level and rendered message of every event in <see cref="CommonApiLogs"/>.
/// </summary>
/// <remarks>
/// A log event is a published contract in the same way an error code is. Dashboards filter
/// on the id, alerts fire on the level, and a saved query matches the message template.
/// None of that is visible from the call site, so a reworded template or a level lowered
/// "to reduce noise" would otherwise reach production and silently switch off an alert.
/// These tests make it a failing build and a conversation instead.
/// </remarks>
public class CommonApiLogsTests
{
    private readonly FakeLogger _logger = new();

    [Fact]
    public void UnhandledException_IsEvent1000()
    {
        var exception = new InvalidOperationException("boom");

        _logger.UnhandledException(exception, "POST", "/api/users/register");

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(1_000, record.Id.Id);
        Assert.Equal(nameof(CommonApiLogs.UnhandledException), record.Id.Name);
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Equal("Unhandled exception while processing POST /api/users/register", record.Message);
        Assert.Same(exception, record.Exception);
    }

    /// <summary>
    /// Every event in the catalogue is pinned by a test above.
    /// </summary>
    /// <remarks>
    /// Without this, adding an event to the catalogue and forgetting its test is a silent
    /// omission — the suite still passes, and the new event is the one nobody checked.
    /// </remarks>
    [Fact]
    public void EveryEvent_HasATest()
    {
        var eventNames = typeof(CommonApiLogs)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.GetCustomAttribute<LoggerMessageAttribute>() is not null)
            .Select(method => method.Name);

        var testNames = typeof(CommonApiLogsTests)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.GetCustomAttribute<FactAttribute>() is not null)
            .Select(method => method.Name)
            .ToList();

        var untested = eventNames
            .Where(name => !testNames.Exists(testName =>
                testName.StartsWith(name + "_", StringComparison.Ordinal)))
            .ToList();

        Assert.True(
            untested.Count == 0,
            $"These events have no test named <EventName>_...: {string.Join(", ", untested)}");
    }
}
