namespace Modules.Users.PublicApi.Contracts;

/// <summary>
/// The parts of a user that other modules are allowed to see.
/// </summary>
/// <param name="Id">The user's id, used as a foreign key by other modules.</param>
/// <param name="Email">The user's email address.</param>
/// <param name="DisplayName">The name to show in a UI.</param>
/// <remarks>
/// Note what is missing: the password hash, the security stamp, lockout state, audit
/// timestamps. A board needs to draw an avatar and a name; nothing outside this module has
/// a reason to know the rest. Publishing the entity instead would leak all of it, and make
/// every field a compatibility promise.
/// </remarks>
public sealed record UserSummary(string Id, string Email, string DisplayName);
