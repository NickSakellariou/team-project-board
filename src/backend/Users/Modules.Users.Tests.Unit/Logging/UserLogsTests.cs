using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Modules.Users.Domain.Logging;

namespace Modules.Users.Tests.Unit.Logging;

/// <summary>
/// Pins the id, name, level and rendered message of every event in <see cref="UserLogs"/>.
/// </summary>
/// <remarks>
/// <para>
/// A log event is a published contract in the same way an error code is. Dashboards filter
/// on the id, alerts fire on the level, and a saved query matches the message template.
/// None of that is visible from the call site, so a reworded template or a level lowered
/// "to reduce noise" would otherwise reach production and silently switch off an alert.
/// </para>
/// <para>
/// This module's catalogue also carries the security-sensitive events, where the level is
/// the whole point: <see cref="UserLogs.RefreshTokenReplayDetected"/> at Warning is what a
/// possible stolen-token alert is built on.
/// </para>
/// </remarks>
public class UserLogsTests
{
    private const string UserId = "bcc47b92-0c1e-4f0a-9a3b-2f7d1b4c8e55";
    private const string CallerId = "1f2e3d4c-5b6a-4790-8c1d-2e3f4a5b6c7d";

    private readonly FakeLogger _logger = new();

    // ----------------------------------------------------------------------------------
    // Authentication
    // ----------------------------------------------------------------------------------

    [Fact]
    public void LoginAttemptedForUnknownEmail_IsEvent10000()
    {
        _logger.LoginAttemptedForUnknownEmail("nobody@example.com");

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(10_000, record.Id.Id);
        Assert.Equal(nameof(UserLogs.LoginAttemptedForUnknownEmail), record.Id.Name);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal("Login attempted for unknown email nobody@example.com", record.Message);
    }

    [Fact]
    public void LoginAttemptedForLockedOutUser_IsEvent10001()
    {
        _logger.LoginAttemptedForLockedOutUser(UserId);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(10_001, record.Id.Id);
        Assert.Equal(nameof(UserLogs.LoginAttemptedForLockedOutUser), record.Id.Name);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal($"Login attempted for locked-out user {UserId}", record.Message);
    }

    [Fact]
    public void LoginFailed_IsEvent10002()
    {
        _logger.LoginFailed(UserId);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(10_002, record.Id.Id);
        Assert.Equal(nameof(UserLogs.LoginFailed), record.Id.Name);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal($"Failed login for user {UserId}", record.Message);
    }

    [Fact]
    public void UserSignedIn_IsEvent10003()
    {
        _logger.UserSignedIn(UserId);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(10_003, record.Id.Id);
        Assert.Equal(nameof(UserLogs.UserSignedIn), record.Id.Name);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal($"User {UserId} signed in", record.Message);
    }

    [Fact]
    public void RefreshTokenNotFound_IsEvent10004()
    {
        _logger.RefreshTokenNotFound(UserId);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(10_004, record.Id.Id);
        Assert.Equal(nameof(UserLogs.RefreshTokenNotFound), record.Id.Name);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal($"Refresh attempted with an unknown token for user {UserId}", record.Message);
    }

    [Fact]
    public void RefreshTokenReplayDetected_IsEvent10005()
    {
        _logger.RefreshTokenReplayDetected(UserId);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(10_005, record.Id.Id);
        Assert.Equal(nameof(UserLogs.RefreshTokenReplayDetected), record.Id.Name);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal(
            $"Refresh token replay detected for user {UserId}. Invalidating all of their tokens.",
            record.Message);
    }

    [Fact]
    public void RefreshTokenAccessTokenMismatch_IsEvent10006()
    {
        _logger.RefreshTokenAccessTokenMismatch(UserId);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(10_006, record.Id.Id);
        Assert.Equal(nameof(UserLogs.RefreshTokenAccessTokenMismatch), record.Id.Name);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal($"Refresh token does not match the supplied access token for user {UserId}", record.Message);
    }

    [Fact]
    public void AccessTokenRejectedDuringRefresh_IsEvent10007()
    {
        var exception = new InvalidOperationException("malformed token");

        _logger.AccessTokenRejectedDuringRefresh(exception);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(10_007, record.Id.Id);
        Assert.Equal(nameof(UserLogs.AccessTokenRejectedDuringRefresh), record.Id.Name);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal("Rejected an access token during refresh", record.Message);
        Assert.Same(exception, record.Exception);
    }

    // ----------------------------------------------------------------------------------
    // Account lifecycle
    // ----------------------------------------------------------------------------------

    [Fact]
    public void RegistrationRejected_IsEvent10100()
    {
        _logger.RegistrationRejected("taken@example.com", "DuplicateEmail, PasswordTooShort");

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(10_100, record.Id.Id);
        Assert.Equal(nameof(UserLogs.RegistrationRejected), record.Id.Name);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(
            "Registration rejected for taken@example.com: DuplicateEmail, PasswordTooShort",
            record.Message);
    }

    [Fact]
    public void DefaultRoleAssignmentFailed_IsEvent10101()
    {
        _logger.DefaultRoleAssignmentFailed(UserId);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(10_101, record.Id.Id);
        Assert.Equal(nameof(UserLogs.DefaultRoleAssignmentFailed), record.Id.Name);
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Equal(
            $"Created user {UserId} but could not assign the default role. The account was rolled back.",
            record.Message);
    }

    [Fact]
    public void UserRegistered_IsEvent10102()
    {
        _logger.UserRegistered(UserId);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(10_102, record.Id.Id);
        Assert.Equal(nameof(UserLogs.UserRegistered), record.Id.Name);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal($"Registered user {UserId}", record.Message);
    }

    [Fact]
    public void UserProfileUpdated_IsEvent10103()
    {
        _logger.UserProfileUpdated(UserId);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(10_103, record.Id.Id);
        Assert.Equal(nameof(UserLogs.UserProfileUpdated), record.Id.Name);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal($"Updated profile for user {UserId}", record.Message);
    }

    [Fact]
    public void UserRoleChanged_IsEvent10104()
    {
        _logger.UserRoleChanged(UserId, "Admin", CallerId);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(10_104, record.Id.Id);
        Assert.Equal(nameof(UserLogs.UserRoleChanged), record.Id.Name);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal($"User {UserId} role changed to Admin by {CallerId}", record.Message);
    }

    [Fact]
    public void UserDeleted_IsEvent10105()
    {
        _logger.UserDeleted(UserId, CallerId);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(10_105, record.Id.Id);
        Assert.Equal(nameof(UserLogs.UserDeleted), record.Id.Name);
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal($"User {UserId} was deleted by {CallerId}", record.Message);
    }

    // ----------------------------------------------------------------------------------
    // The catalogue as a whole
    // ----------------------------------------------------------------------------------

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
        var eventNames = typeof(UserLogs)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.GetCustomAttribute<LoggerMessageAttribute>() is not null)
            .Select(method => method.Name);

        var testNames = typeof(UserLogsTests)
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

    /// <summary>
    /// A successful sign-in records the user id and nothing that identifies them further.
    /// </summary>
    /// <remarks>
    /// The rule from docs/concepts/observability.md, stated as a test: the email appears on
    /// the failure path, where it is the only thing there is to record, and never once an
    /// account is known.
    /// </remarks>
    [Fact]
    public void UserSignedIn_DoesNotRecordTheEmail()
    {
        _logger.UserSignedIn(UserId);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.DoesNotContain("@", record.Message, StringComparison.Ordinal);
    }
}
