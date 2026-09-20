namespace Modules.Users.Domain.Users;

/// <summary>
/// The system-wide roles. Distinct from project roles (Owner / Member), which the
/// Projects module owns.
/// </summary>
/// <remarks>
/// <para>
/// Two levels of role exist in this application and they answer different questions.
/// A <b>system role</b> says what you are in the application as a whole. A <b>project
/// role</b> says what you are inside one project — you can own one project and be a
/// plain member of another, so it cannot live on the user.
/// </para>
/// <para>
/// Constants rather than an enum because ASP.NET Core Identity stores role names as
/// strings, and an enum would need converting at every boundary.
/// </para>
/// </remarks>
public static class SystemRoles
{
    /// <summary>Can administer users. Not automatically a member of every project.</summary>
    public const string Admin = "Admin";

    /// <summary>An ordinary user. Every account gets this on registration.</summary>
    public const string User = "User";

    /// <summary>Gets every system role, for seeding.</summary>
    public static IReadOnlyList<string> All { get; } = [Admin, User];
}
