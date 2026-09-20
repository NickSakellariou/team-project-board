using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using TeamProjectBoard.Host.Logging;

namespace TeamProjectBoard.Host.Tests.Unit.Logging;

/// <summary>
/// Pins the id, name, level and rendered message of every event in <see cref="HostLogs"/>.
/// </summary>
/// <remarks>
/// A log event is a published contract in the same way an error code is. Dashboards filter
/// on the id, alerts fire on the level, and a saved query matches the message template.
/// None of that is visible from the call site, so a reworded template or a level lowered
/// "to reduce noise" would otherwise reach production and silently switch off an alert.
/// </remarks>
public class HostLogsTests
{
    private readonly FakeLogger _logger = new();

    [Fact]
    public void RoleCreated_IsEvent4000()
    {
        _logger.RoleCreated("Admin");

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(4_000, record.Id.Id);
        Assert.Equal(nameof(HostLogs.RoleCreated), record.Id.Name);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal("Created role Admin", record.Message);
    }

    [Fact]
    public void PermissionGranted_IsEvent4001()
    {
        _logger.PermissionGranted("users:delete", "Admin");

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(4_001, record.Id.Id);
        Assert.Equal(nameof(HostLogs.PermissionGranted), record.Id.Name);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal("Granted users:delete to the Admin role", record.Message);
    }

    [Fact]
    public void SeedAdminNotConfigured_IsEvent4002()
    {
        _logger.SeedAdminNotConfigured();

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(4_002, record.Id.Id);
        Assert.Equal(nameof(HostLogs.SeedAdminNotConfigured), record.Id.Name);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal("No seed admin configured; skipping.", record.Message);
    }

    [Fact]
    public void SeedAdminCreationFailed_IsEvent4003()
    {
        _logger.SeedAdminCreationFailed("Passwords must have at least one digit.");

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(4_003, record.Id.Id);
        Assert.Equal(nameof(HostLogs.SeedAdminCreationFailed), record.Id.Name);
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Equal(
            "Could not seed the admin account: Passwords must have at least one digit.",
            record.Message);
    }

    /// <summary>
    /// The seeded-admin event is a Warning, not an Information.
    /// </summary>
    /// <remarks>
    /// Deliberate, and the reason this assertion is called out rather than left implicit:
    /// an account with a known password now exists. If this line ever appears outside
    /// Development it is an incident, and it has to be loud enough to be noticed.
    /// </remarks>
    [Fact]
    public void SeededDevelopmentAdmin_IsEvent4004AtWarning()
    {
        _logger.SeededDevelopmentAdmin("admin@example.com");

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(4_004, record.Id.Id);
        Assert.Equal(nameof(HostLogs.SeededDevelopmentAdmin), record.Id.Name);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal(
            "Seeded development admin admin@example.com. This account exists only in Development.",
            record.Message);
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
        var eventNames = typeof(HostLogs)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.GetCustomAttribute<LoggerMessageAttribute>() is not null)
            .Select(method => method.Name);

        var testNames = typeof(HostLogsTests)
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
