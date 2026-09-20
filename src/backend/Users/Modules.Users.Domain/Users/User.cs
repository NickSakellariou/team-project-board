using Microsoft.AspNetCore.Identity;
using Modules.Common.Domain;

namespace Modules.Users.Domain.Users;

/// <summary>
/// A person with an account.
/// </summary>
/// <remarks>
/// <para>
/// <b>What <see cref="IdentityUser"/> gives us.</b> Deriving from it inherits the columns
/// ASP.NET Core Identity needs to do its job, so we never write them:
/// </para>
/// <list type="bullet">
///   <item><c>Id</c> — the primary key (a string; we store a GUID in it).</item>
///   <item><c>UserName</c> / <c>NormalizedUserName</c> and <c>Email</c> /
///   <c>NormalizedEmail</c> — the normalized copies are upper-cased so lookups are
///   case-insensitive without a database collation trick, and so a unique index can stop
///   "Nick@x.com" and "nick@x.com" both registering.</item>
///   <item><c>PasswordHash</c> — never the password. See docs/concepts/aspnet-core-identity.md
///   for what Identity actually stores here.</item>
///   <item><c>SecurityStamp</c> — a random value regenerated whenever credentials change.
///   It is how "log out everywhere after a password change" is implemented.</item>
///   <item><c>ConcurrencyStamp</c> — optimistic concurrency, so two simultaneous edits to
///   one user cannot silently overwrite each other.</item>
///   <item><c>EmailConfirmed</c>, <c>PhoneNumber</c>, <c>TwoFactorEnabled</c>,
///   <c>LockoutEnd</c>, <c>AccessFailedCount</c> — features we do not use yet, but which
///   cost nothing to carry and are painful to retrofit.</item>
/// </list>
/// <para>
/// <b>What we add.</b> Only auditing. Everything else a user needs, they already have.
/// Note what is deliberately absent: no <c>Projects</c> collection. Project membership
/// belongs to the Projects module, which references users by id across a schema boundary.
/// A navigation property here would let a Users query silently join into another module's
/// tables — the exact coupling the module structure exists to prevent.
/// </para>
/// </remarks>
public sealed class User : IdentityUser, IAuditableEntity
{
    /// <summary>Gets or sets the user's display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <inheritdoc />
    public DateTime CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public DateTime? UpdatedAtUtc { get; set; }
}
