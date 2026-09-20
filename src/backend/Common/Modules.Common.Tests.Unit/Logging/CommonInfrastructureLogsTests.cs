using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Modules.Common.Infrastructure.Logging;

namespace Modules.Common.Tests.Unit.Logging;

/// <summary>
/// Pins the id, name, level and rendered message of every event in
/// <see cref="CommonInfrastructureLogs"/>. See <see cref="CommonApiLogsTests"/> for why.
/// </summary>
public class CommonInfrastructureLogsTests
{
    private readonly FakeLogger _logger = new();

    [Fact]
    public void AuthorizationPoliciesRegistered_IsEvent3000()
    {
        _logger.AuthorizationPoliciesRegistered(4, "Users");

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(3_000, record.Id.Id);
        Assert.Equal(nameof(CommonInfrastructureLogs.AuthorizationPoliciesRegistered), record.Id.Name);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal("Registered 4 authorization policies for module Users", record.Message);
    }

    /// <summary>Every event in the catalogue is pinned by a test above.</summary>
    [Fact]
    public void EveryEvent_HasATest()
    {
        var eventNames = typeof(CommonInfrastructureLogs)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.GetCustomAttribute<LoggerMessageAttribute>() is not null)
            .Select(method => method.Name);

        var testNames = typeof(CommonInfrastructureLogsTests)
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
