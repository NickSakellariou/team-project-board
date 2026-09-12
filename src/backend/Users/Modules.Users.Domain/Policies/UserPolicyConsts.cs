namespace Modules.Users.Domain.Policies;

/// <summary>
/// The permissions the Users module defines.
/// </summary>
/// <remarks>
/// <para>
/// These are <b>claims</b>, not roles, and endpoints are guarded by the claim rather than
/// by "is this user an Admin". The difference matters: <c>RequireRole("Admin")</c> hard-codes
/// today's org chart into the endpoint, so introducing a "Support" role that may read users
/// but not delete them means editing every endpoint. With claims, the endpoint states the
/// permission it needs and the roles that carry it are configuration.
/// </para>
/// <para>
/// The names are colon-separated (<c>users:read</c>) so permissions from different modules
/// cannot collide — Boards will define <c>boards:read</c>.
/// See docs/concepts/authentication-and-jwt.md.
/// </para>
/// </remarks>
public static class UserPolicyConsts
{
    /// <summary>Read any user's details, not just your own.</summary>
    public const string Read = "users:read";

    /// <summary>Create users administratively.</summary>
    public const string Create = "users:create";

    /// <summary>Modify any user, including their system role.</summary>
    public const string Update = "users:update";

    /// <summary>Delete a user.</summary>
    public const string Delete = "users:delete";

    /// <summary>Gets every permission this module defines.</summary>
    public static IReadOnlyList<string> All { get; } = [Read, Create, Update, Delete];
}
