using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Modules.Users.Domain.Tokens;
using Modules.Users.Domain.Users;

namespace Modules.Users.Infrastructure.Database;

/// <summary>
/// The Users module's database session.
/// </summary>
/// <remarks>
/// <para>
/// A <c>DbContext</c> is two things at once: a unit of work (it batches every change and
/// writes them in one transaction on <c>SaveChangesAsync</c>) and an identity map (loading
/// the same row twice yields the same object). Both are why it is registered scoped — one
/// per HTTP request — rather than as a singleton.
/// </para>
/// <para>
/// Deriving from <see cref="IdentityDbContext{TUser,TRole,TKey}"/> brings the seven tables
/// Identity needs. Only two are interesting to us:
/// </para>
/// <list type="bullet">
///   <item><c>users</c> and <c>roles</c> — the entities we own.</item>
///   <item><c>user_roles</c> — who has which role.</item>
///   <item><c>user_claims</c>, <c>role_claims</c> — permissions. We attach the
///   <c>users:*</c> claims to roles, so <c>role_claims</c> is what the policy checks
///   ultimately resolve to.</item>
///   <item><c>user_logins</c> — external providers ("Sign in with Google"). Unused; it
///   stays empty and costs nothing.</item>
///   <item><c>user_tokens</c> — Identity's own store for password-reset and 2FA tokens.
///   Not related to our JWTs, and also unused so far.</item>
/// </list>
/// <para>
/// The generic arguments say: our <see cref="User"/>, our <see cref="Role"/>, and
/// <see cref="string"/> keys. The five join types are left as Identity's defaults, since
/// we add nothing to them.
/// </para>
/// </remarks>
public sealed class UsersDbContext(DbContextOptions<UsersDbContext> options)
    : IdentityDbContext<User, Role, string>(options)
{
    /// <summary>Gets the refresh tokens issued by this module.</summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Must run first: the base method configures all the Identity entities, and our
        // own configuration below adjusts what it produced.
        base.OnModelCreating(builder);

        // Everything this context maps goes in the users schema, including the Identity
        // tables we did not write.
        builder.HasDefaultSchema(DbConsts.Schema);

        // Picks up every IEntityTypeConfiguration in this assembly, so adding a new
        // entity's mapping file is all that is needed to register it.
        builder.ApplyConfigurationsFromAssembly(typeof(UsersDbContext).Assembly);

        RenameIdentityTables(builder);
    }

    // Why: Identity's defaults are AspNetUsers, AspNetRoles, AspNetUserClaims and so on —
    // PascalCase, prefixed with a framework name that means nothing to someone reading the
    // database. UseSnakeCaseNamingConvention (see AddUsersInfrastructure) fixes the column
    // names but keeps the type name as the table name, so we set the table names here and
    // let the convention lower-case them. The result reads as plain SQL: users.user,
    // users.role, users.user_role.
    private static void RenameIdentityTables(ModelBuilder builder)
    {
        builder.Entity<User>().ToTable("user");
        builder.Entity<Role>().ToTable("role");
        builder.Entity<IdentityUserRole<string>>().ToTable("user_role");
        builder.Entity<IdentityUserClaim<string>>().ToTable("user_claim");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("role_claim");
        builder.Entity<IdentityUserLogin<string>>().ToTable("user_login");
        builder.Entity<IdentityUserToken<string>>().ToTable("user_token");
    }
}
