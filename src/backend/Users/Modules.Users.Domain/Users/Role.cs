using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;

namespace Modules.Users.Domain.Users;

/// <summary>
/// A system-wide role. See <see cref="SystemRoles"/> for the values that exist.
/// </summary>
/// <remarks>
/// Derives from <see cref="IdentityRole"/> for <c>Id</c>, <c>Name</c> and
/// <c>NormalizedName</c>. We add nothing — the type exists so <c>RoleManager&lt;Role&gt;</c>
/// is strongly typed against our own class, leaving room to add fields later without a
/// migration of every generic signature in the module.
/// </remarks>
[SuppressMessage("Major Code Smell", "S2094:Classes should not be empty",
    Justification = "Deriving from IdentityRole is what makes RoleManager<Role> strongly typed " +
                    "against this module. Using IdentityRole directly would work today but would " +
                    "put a framework type in every generic signature in the module, so adding a " +
                    "single field later becomes a change to all of them.")]
public sealed class Role : IdentityRole;
