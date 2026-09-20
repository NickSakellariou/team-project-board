namespace TeamProjectBoard.Host.Logging;

/// <summary>
/// Every log event emitted by the composition root.
/// </summary>
/// <remarks>
/// <para>
/// Startup and seeding events. They belong to the Host rather than to a module because
/// they describe what the application did while wiring itself up — the Users module has no
/// opinion about whether a development admin was seeded.
/// </para>
/// <para>
/// See <c>docs/log-event-ids.md</c> for the range this catalogue owns and how ids are
/// allocated across the solution.
/// </para>
/// </remarks>
public static partial class HostLogs
{
    /// <summary>The first event id reserved for this catalogue.</summary>
    public const int EventIdRangeStart = 4_000;

    /// <summary>The last event id reserved for this catalogue.</summary>
    public const int EventIdRangeEnd = 4_999;

    /// <summary>A system role did not exist and was created.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="roleName">The role that was created.</param>
    [LoggerMessage(
        EventId = 4_000,
        Level = LogLevel.Information,
        Message = "Created role {RoleName}")]
    public static partial void RoleCreated(this ILogger logger, string roleName);

    /// <summary>A permission claim was attached to a role.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="permission">The permission claim type, e.g. <c>users:delete</c>.</param>
    /// <param name="roleName">The role that was granted it.</param>
    [LoggerMessage(
        EventId = 4_001,
        Level = LogLevel.Information,
        Message = "Granted {Permission} to the {RoleName} role")]
    public static partial void PermissionGranted(this ILogger logger, string permission, string roleName);

    /// <summary>No seed admin credentials were configured, so none was created.</summary>
    /// <param name="logger">The logger to write to.</param>
    [LoggerMessage(
        EventId = 4_002,
        Level = LogLevel.Information,
        Message = "No seed admin configured; skipping.")]
    public static partial void SeedAdminNotConfigured(this ILogger logger);

    /// <summary>Identity refused to create the seed admin account.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="identityErrors">Identity's own error descriptions, comma separated.</param>
    [LoggerMessage(
        EventId = 4_003,
        Level = LogLevel.Error,
        Message = "Could not seed the admin account: {IdentityErrors}")]
    public static partial void SeedAdminCreationFailed(this ILogger logger, string identityErrors);

    /// <summary>
    /// A development admin account was created.
    /// </summary>
    /// <remarks>
    /// Warning rather than Information on purpose: an account with a known password now
    /// exists, and if this line ever appears outside Development it is an incident.
    /// </remarks>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="email">The seeded account's email.</param>
    [LoggerMessage(
        EventId = 4_004,
        Level = LogLevel.Warning,
        Message = "Seeded development admin {Email}. This account exists only in Development.")]
    public static partial void SeededDevelopmentAdmin(this ILogger logger, string email);
}
