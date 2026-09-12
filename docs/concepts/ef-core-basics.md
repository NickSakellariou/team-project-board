# EF Core

## What it is

An **object-relational mapper**: it turns C# objects into database rows and back, and
translates LINQ into SQL.

```csharp
await dbContext.Users.Where(u => u.Email == email).ToListAsync();
```

becomes

```sql
SELECT id, email, display_name, ... FROM users."user" WHERE email = @__email_0
```

The parameter matters: EF parameterises every value. It is structurally very hard to write
a SQL injection through LINQ, which is one of the quieter benefits.

## `DbContext` is two things

**A unit of work.** It accumulates changes and writes them in one transaction when you call
`SaveChangesAsync`. Add three entities, save once, and either all three land or none do.

**An identity map.** Load the same row twice in one context and you get the *same object*,
not two copies. So two parts of a request cannot hold divergent versions of one row.

Both are why it is registered **scoped** — one per HTTP request, so a request is one unit
of work. See [dependency-injection.md](dependency-injection.md) for why singleton would be
a disaster.

## Change tracking

The context watches every entity it loads or is given, and works out what SQL is needed:

```csharp
var user = await userManager.FindByIdAsync(userId);   // now tracked, state = Unchanged
user.DisplayName = "Renamed";                          // state = Modified
await dbContext.SaveChangesAsync();                    // UPDATE ... SET display_name = ...
```

Nobody told EF which property changed. It compares against the snapshot taken at load time
and generates an UPDATE touching only that column.

This is convenient and not free: the snapshot costs memory and comparison time. For a read
you never intend to modify, opt out:

```csharp
.AsNoTracking()
```

`UserModuleApi.GetUsersAsync` does exactly that. On a query returning many rows it is a
meaningful saving, and it removes a whole class of accidental-write bug.

### Projection is better still

```csharp
.Select(user => new UserSummary(user.Id, user.Email!, user.DisplayName))
```

This puts the projection *in the SQL*: `SELECT id, email, display_name`. The password hash
never leaves the database. Without it, EF selects every column, materialises full `User`
objects, and the mapping happens in memory.

## Configuration lives outside the entity

The `User` entity has no attributes on it — no `[Table]`, no `[MaxLength]`. Mapping is in a
separate class:

```csharp
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(user => user.DisplayName).HasMaxLength(128).IsRequired();
        builder.HasIndex(user => user.NormalizedEmail).IsUnique();
    }
}
```

That is what keeps `Modules.Users.Domain` free of persistence concerns: the entity does not
know it is stored, let alone how wide its columns are. `ApplyConfigurationsFromAssembly`
picks up every such class, so adding an entity's mapping file is all that is needed to
register it.

### Why set a max length at all

Without one, Npgsql maps a `string` to `text` — unbounded. It works, but the database
enforces nothing, and a bug or a malicious client can store a megabyte in a display name.
A length is a cheap constraint at the only layer that cannot be bypassed.

## Schemas and why each module gets one

```csharp
builder.HasDefaultSchema(DbConsts.Schema);   // "users"
```

Every table this context maps goes in the `users` schema. Boards will use `boards`. Nothing
lands in `public`.

The schema is the module boundary made physical. All modules share one database — one
connection, one backup, real transactions — but a `DbContext` scoped to one schema cannot
see another's tables, so the convenient cross-module join that quietly destroys a modular
monolith is not available.

### Foreign keys stop at the schema line

`RefreshTokenConfiguration` has a foreign key to `User` with cascade delete. Both tables are
in `users`, so that is fine and desirable — deleting a user must not leave redeemable
tokens behind.

When Projects stores a `UserId`, there will be **no** foreign key. That crosses a module
boundary, and an FK there would mean the two modules could never change storage
independently. The integrity that is lost has to be recovered in application code, via
`IUserModuleApi.UserExistsAsync`. A real cost, accepted deliberately —
[ADR-0007](../adr/0007-schema-per-module.md).

## Naming conventions

```csharp
.UseSnakeCaseNamingConvention()
```

EF's default takes the C# name verbatim: `NormalizedEmail` becomes a column called
`NormalizedEmail`. In Postgres, unquoted identifiers are folded to lower case, so any
hand-written query has to say `"NormalizedEmail"` with the quotes, every time. Miss them
and you get a confusing "column normalizedemail does not exist".

snake_case is the Postgres convention, and the package applies it to every column and
table, so the database stays pleasant to use outside of EF.

Identity's table names need one extra step. The convention lower-cases the *type* name, so
`AspNetUsers` would become `asp_net_users`. `UsersDbContext.RenameIdentityTables` sets them
explicitly, and the result reads as plain SQL: `users.user`, `users.role`,
`users.user_role`.

## Migrations

A migration is a versioned, generated description of a schema change.

```bash
dotnet ef migrations add InitialUsers \
  --project Users/Modules.Users.Infrastructure \
  --startup-project TeamProjectBoard.Host \
  --output-dir Database/Migrations
```

Three files appear:

- `<timestamp>_InitialUsers.cs` — `Up()` applies the change, `Down()` reverses it.
- `<timestamp>_InitialUsers.Designer.cs` — the model as of this migration.
- `UsersDbContextModelSnapshot.cs` — the current model, used to diff the *next* migration.

**Two projects, because they answer different questions.** `--project` says where to write
the migration; `--startup-project` says where to find the DI container that knows the
connection string and provider.

### A per-module history table

```csharp
npgsql.MigrationsHistoryTable(DbConsts.MigrationHistoryTable, DbConsts.Schema)
```

EF records applied migrations in a table so it knows what to skip. The default is one
shared `__EFMigrationsHistory` in `public` — which for a modular monolith means every
module's history interleaved in one table, each treating the others' entries as unknown.
One per schema keeps them independent.

### Do not hand-edit a migration

The snapshot records what the model looked like. Edit the migration without updating the
snapshot and the *next* migration is generated against a model that no longer matches
reality. Migrations are also excluded from our analyzer rules for this reason — the
`.editorconfig` marks them `generated_code = true`.

### Why we only migrate in Development

```csharp
if (app.Environment.IsDevelopment())
{
    await scope.MigrateModuleDatabasesAsync();
}
```

Convenient locally. Wrong in production: several instances starting at once would race each
other, and a failed migration takes the application down instead of failing the deploy.
There, migrations are a deployment step that runs once, before the new version starts.

## Interceptors

`AuditableInterceptor` hooks the `SaveChanges` pipeline and stamps timestamps:

```csharp
foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
{
    if (entry.State == EntityState.Added) entry.Entity.CreatedAtUtc = timestamp;
    ...
}
```

An entity implements `IAuditableEntity` and gets audited. No handler writes
`entity.CreatedAtUtc = DateTime.UtcNow`, so nobody can forget it in one of thirty handlers.

One subtlety worth noting:

```csharp
entry.Property(nameof(IAuditableEntity.CreatedAtUtc)).IsModified = false;
```

Without it, an UPDATE would include `created_at_utc` and overwrite the original creation
time with whatever the loaded entity happened to hold.

## `ExecuteUpdateAsync`

```csharp
await dbContext.RefreshTokens
    .Where(token => token.UserId == userId && !token.Invalidated)
    .ExecuteUpdateAsync(s => s.SetProperty(t => t.Invalidated, true), cancellationToken);
```

One `UPDATE ... WHERE` statement. The alternative — load every token, set a property on
each, save — would fetch rows into memory only to write them back.

The catch, worth knowing: it **bypasses the change tracker and interceptors**. Entities
already loaded in this context will not see the change, and `AuditableInterceptor` does not
run, so `UpdatedAtUtc` is not stamped. Fine here; a trap if you assume the usual machinery
applies.

## Related

- [dependency-injection.md](dependency-injection.md) — why the DbContext is scoped
- [aspnet-core-identity.md](aspnet-core-identity.md) — the entities being mapped
- [testing-strategy.md](testing-strategy.md) — why tests use a real Postgres
