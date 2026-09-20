using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Modules.Common.Application.Logging;

namespace Modules.Common.Tests.Unit.Logging;

/// <summary>
/// Pins the id, name, level and rendered message of every event in
/// <see cref="CommonApplicationLogs"/>. See <see cref="CommonApiLogsTests"/> for why.
/// </summary>
public class CommonApplicationLogsTests
{
    private readonly FakeLogger _logger = new();

    [Fact]
    public void ValidationFailed_IsEvent2000()
    {
        _logger.ValidationFailed("Register user", "Email: Must be a valid email address.");

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(2_000, record.Id.Id);
        Assert.Equal(nameof(CommonApplicationLogs.ValidationFailed), record.Id.Name);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal("Register user: Email: Must be a valid email address.", record.Message);
    }

    /// <summary>
    /// The use case and the failures are separate structured fields, not one sentence.
    /// </summary>
    /// <remarks>
    /// The whole reason this event exists once rather than once per slice. If the template
    /// were ever interpolated the rendered line would look identical and
    /// "show me every failed registration" would stop returning anything.
    /// </remarks>
    [Fact]
    public void ValidationFailed_RecordsTheContextAndTheErrorsAsSeparateFields()
    {
        _logger.ValidationFailed("Register user", "Email: Must be a valid email address.");

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.NotNull(record.StructuredState);
        Assert.Contains(
            record.StructuredState,
            field => string.Equals(field.Key, "ContextMessage", StringComparison.Ordinal) &&
                     string.Equals(field.Value, "Register user", StringComparison.Ordinal));
        Assert.Contains(
            record.StructuredState,
            field => string.Equals(field.Key, "ValidationErrors", StringComparison.Ordinal) &&
                     string.Equals(field.Value, "Email: Must be a valid email address.", StringComparison.Ordinal));
    }

    /// <summary>Every event in the catalogue is pinned by a test above.</summary>
    [Fact]
    public void EveryEvent_HasATest()
    {
        var eventNames = typeof(CommonApplicationLogs)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.GetCustomAttribute<LoggerMessageAttribute>() is not null)
            .Select(method => method.Name);

        var testNames = typeof(CommonApplicationLogsTests)
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
