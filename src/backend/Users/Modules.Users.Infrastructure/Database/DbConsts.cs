namespace Modules.Users.Infrastructure.Database;

/// <summary>
/// Database names owned by the Users module.
/// </summary>
public static class DbConsts
{
    /// <summary>
    /// The Postgres schema every table in this module lives in.
    /// </summary>
    /// <remarks>
    /// The schema is the module boundary made physical. All modules share one database —
    /// so a single connection, a single backup, and real transactions — but each owns a
    /// schema, so <c>boards.task</c> cannot accidentally be joined to <c>users.user</c>
    /// without saying so explicitly. Nothing is created in <c>public</c>.
    /// </remarks>
    public const string Schema = "users";

    /// <summary>
    /// The table EF Core records applied migrations in.
    /// </summary>
    /// <remarks>
    /// Each module needs its own, inside its own schema. With the default shared
    /// <c>__EFMigrationsHistory</c>, two modules' migration histories would interleave in
    /// one table and each would consider the other's entries unknown.
    /// </remarks>
    public const string MigrationHistoryTable = "migration_history";
}
