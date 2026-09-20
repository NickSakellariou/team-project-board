using Microsoft.AspNetCore.Identity;
using Modules.Users.Domain.Users;
using NSubstitute;

namespace Modules.Users.Tests.Unit.Configuration;

/// <summary>
/// Builds substitutes for ASP.NET Core Identity's managers.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="UserManager{TUser}"/> and <see cref="RoleManager{TRole}"/> are classes
/// rather than interfaces, so NSubstitute has to call a real constructor before it can
/// intercept anything. Only the store argument is supplied; the rest are unused by the
/// methods the handlers call, and Identity's constructors accept null for every one of
/// them.
/// </para>
/// <para>
/// This works at all because the methods under test — <c>FindByIdAsync</c>,
/// <c>CreateAsync</c>, <c>GetRolesAsync</c> and the rest — are virtual. Substituting a
/// manager is the one concession these tests make to Identity being a framework: the
/// alternative is a database, which is what the integration suite is for.
/// </para>
/// </remarks>
internal static class IdentitySubstitutes
{
    /// <summary>Creates a substitute <see cref="UserManager{TUser}"/>.</summary>
    public static UserManager<User> UserManager() =>
        Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

    /// <summary>Creates a substitute <see cref="RoleManager{TRole}"/>.</summary>
    public static RoleManager<Role> RoleManager() =>
        Substitute.For<RoleManager<Role>>(
            Substitute.For<IRoleStore<Role>>(),
            null,
            null,
            null,
            null);

    /// <summary>Creates a user with the supplied id, and a matching email and name.</summary>
    public static User AUser(string id = "user-1", string displayName = "Nick") => new()
    {
        Id = id,
        Email = $"{id}@example.com",
        UserName = $"{id}@example.com",
        DisplayName = displayName
    };

    /// <summary>Creates a failed <see cref="IdentityResult"/> carrying one error.</summary>
    public static IdentityResult Failed(string code, string description) =>
        IdentityResult.Failed(new IdentityError { Code = code, Description = description });
}
