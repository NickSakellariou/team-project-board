using Microsoft.Extensions.Logging;

namespace Modules.Users.Domain.Logging;

/// <summary>
/// Every log event the Users module can emit.
/// </summary>
/// <remarks>
/// <para>
/// The logging counterpart of <see cref="Modules.Users.Domain.Errors.UserErrors"/>, and for
/// the same reasons: the module's whole observable surface is readable in one file, the
/// wording of an event is changed in one place, and the set of things worth recording is a
/// decision someone made rather than an accident of where a line was typed. It sits in
/// Domain because both Features and Infrastructure emit these — Domain is the one layer
/// both can see.
/// </para>
/// <para>
/// Each method is a <c>[LoggerMessage]</c> declaration: the source generator writes a
/// cached, allocation-free implementation and stamps the event with a stable
/// <see cref="EventId"/>. The id is the point. A message template gets reworded, a method
/// gets renamed, the code moves to another class — the id does not, so a dashboard or an
/// alert built on <c>EventId = 10005</c> keeps working.
/// </para>
/// <para>
/// Ids are allocated from this catalogue's reserved range and never reused, even after an
/// event is deleted. The allocation across the whole solution is in
/// <c>docs/log-event-ids.md</c>; adding one is described by the <c>adding-a-log</c> skill.
/// </para>
/// <para>
/// What must never appear in an argument here: passwords, tokens, password hashes or
/// security stamps. Note that a failed login records the email and a successful one records
/// only the id — see docs/concepts/observability.md for why.
/// </para>
/// </remarks>
public static partial class UserLogs
{
    /// <summary>The first event id reserved for this catalogue.</summary>
    public const int EventIdRangeStart = 10_000;

    /// <summary>The last event id reserved for this catalogue.</summary>
    public const int EventIdRangeEnd = 10_999;

    // ----------------------------------------------------------------------------------
    // Authentication - 10000-10099
    // ----------------------------------------------------------------------------------

    /// <summary>A sign-in was attempted with an email that has no account.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="email">The email the caller supplied.</param>
    [LoggerMessage(
        EventId = 10_000,
        Level = LogLevel.Information,
        Message = "Login attempted for unknown email {Email}")]
    public static partial void LoginAttemptedForUnknownEmail(this ILogger logger, string email);

    /// <summary>A sign-in was attempted against a locked-out account.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="userId">The locked-out user.</param>
    [LoggerMessage(
        EventId = 10_001,
        Level = LogLevel.Warning,
        Message = "Login attempted for locked-out user {UserId}")]
    public static partial void LoginAttemptedForLockedOutUser(this ILogger logger, string userId);

    /// <summary>A sign-in failed because the password did not match.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="userId">The user whose password check failed.</param>
    [LoggerMessage(
        EventId = 10_002,
        Level = LogLevel.Information,
        Message = "Failed login for user {UserId}")]
    public static partial void LoginFailed(this ILogger logger, string userId);

    /// <summary>A user signed in successfully.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="userId">The user who signed in.</param>
    [LoggerMessage(
        EventId = 10_003,
        Level = LogLevel.Information,
        Message = "User {UserId} signed in")]
    public static partial void UserSignedIn(this ILogger logger, string userId);

    /// <summary>A refresh was attempted with a token that is not in the database.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="userId">The user named by the expired access token.</param>
    [LoggerMessage(
        EventId = 10_004,
        Level = LogLevel.Warning,
        Message = "Refresh attempted with an unknown token for user {UserId}")]
    public static partial void RefreshTokenNotFound(this ILogger logger, string userId);

    /// <summary>
    /// An already-used refresh token was presented again, so every token the user holds was
    /// invalidated.
    /// </summary>
    /// <remarks>
    /// The one event in this catalogue worth alerting on. A replay is either a buggy client
    /// or a stolen token, and the two are indistinguishable from here.
    /// </remarks>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="userId">The user whose token chain was cut off.</param>
    [LoggerMessage(
        EventId = 10_005,
        Level = LogLevel.Warning,
        Message = "Refresh token replay detected for user {UserId}. Invalidating all of their tokens.")]
    public static partial void RefreshTokenReplayDetected(this ILogger logger, string userId);

    /// <summary>A refresh token was presented alongside an access token it was not issued with.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="userId">The user named by the access token.</param>
    [LoggerMessage(
        EventId = 10_006,
        Level = LogLevel.Warning,
        Message = "Refresh token does not match the supplied access token for user {UserId}")]
    public static partial void RefreshTokenAccessTokenMismatch(this ILogger logger, string userId);

    /// <summary>An access token presented for refresh failed validation.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="exception">The validation failure.</param>
    [LoggerMessage(
        EventId = 10_007,
        Level = LogLevel.Information,
        Message = "Rejected an access token during refresh")]
    public static partial void AccessTokenRejectedDuringRefresh(this ILogger logger, Exception exception);

    // ----------------------------------------------------------------------------------
    // Account lifecycle - 10100-10199
    // ----------------------------------------------------------------------------------

    /// <summary>Identity refused to create the account.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="email">The email that was registered.</param>
    /// <param name="identityErrors">Identity's own error codes, comma separated.</param>
    [LoggerMessage(
        EventId = 10_100,
        Level = LogLevel.Information,
        Message = "Registration rejected for {Email}: {IdentityErrors}")]
    public static partial void RegistrationRejected(this ILogger logger, string email, string identityErrors);

    /// <summary>
    /// An account was created but could not be given the default role, so it was rolled back.
    /// </summary>
    /// <remarks>
    /// Error rather than Warning: this only fires when role seeding never ran, which means
    /// the application is misconfigured and no registration can succeed.
    /// </remarks>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="userId">The account that was created and then removed.</param>
    [LoggerMessage(
        EventId = 10_101,
        Level = LogLevel.Error,
        Message = "Created user {UserId} but could not assign the default role. The account was rolled back.")]
    public static partial void DefaultRoleAssignmentFailed(this ILogger logger, string userId);

    /// <summary>An account was created.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="userId">The new user.</param>
    [LoggerMessage(
        EventId = 10_102,
        Level = LogLevel.Information,
        Message = "Registered user {UserId}")]
    public static partial void UserRegistered(this ILogger logger, string userId);

    /// <summary>A user's profile was updated.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="userId">The user whose profile changed.</param>
    [LoggerMessage(
        EventId = 10_103,
        Level = LogLevel.Information,
        Message = "Updated profile for user {UserId}")]
    public static partial void UserProfileUpdated(this ILogger logger, string userId);

    /// <summary>An admin changed a user's system role.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="userId">The user whose role changed.</param>
    /// <param name="role">The role they now hold.</param>
    /// <param name="callerId">The admin who made the change.</param>
    [LoggerMessage(
        EventId = 10_104,
        Level = LogLevel.Information,
        Message = "User {UserId} role changed to {Role} by {CallerId}")]
    public static partial void UserRoleChanged(this ILogger logger, string userId, string role, string callerId);

    /// <summary>An admin deleted a user account.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="userId">The deleted user.</param>
    /// <param name="callerId">The admin who deleted them.</param>
    [LoggerMessage(
        EventId = 10_105,
        Level = LogLevel.Information,
        Message = "User {UserId} was deleted by {CallerId}")]
    public static partial void UserDeleted(this ILogger logger, string userId, string callerId);
}
