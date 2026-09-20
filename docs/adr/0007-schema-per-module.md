# 0007. One database, one schema and migration history per module

**Status:** Accepted
**Date:** 2026-09-13

## Context

Four modules need to store data. Sharing a database is how modular monoliths usually die:
someone writes one convenient join across two modules' tables, and from then on neither
module can change its storage independently.

## Options considered

**One database, one shared schema.** Simplest. Nothing stops a cross-module join, and
nothing signals that one is a mistake. The boundary exists only in the folder names.

**A database per module.** Boundaries are as hard as they get. Also four connection
strings, four backup schedules, and no transaction spanning modules — most of the cost of
microservices while still deploying one process.

**One database, a schema per module.** Each module gets a Postgres schema, its own
`DbContext`, and its own migration-history table. One connection, one backup, real
transactions — but a context scoped to `users` cannot see `boards`.

## Decision

One database. Each module owns a schema (`users`, later `projects`, `boards`), a
`DbContext`, and a `migration_history` table inside its own schema. Nothing is created in
`public`.

**No foreign keys across schemas.** When Projects stores a `UserId`, it is a plain string
with no FK.

## Consequences

**Easier**

- The boundary is physical at the data layer, which is where it matters most: the
  convenient cross-module join is not available, because the other schema's tables are not
  mapped.
- One connection string, one backup, one restore.
- Real ACID transactions within a module.
- Each module migrates independently — a separate history table means one module's
  migrations are not unknown entries in another's.
- A module could be extracted to its own database later by moving one schema.

**Harder**

- **Referential integrity stops at the schema line.** Nothing prevents a project membership
  row pointing at a deleted user. That has to be compensated in application code, via
  `IUserModuleApi.UserExistsAsync` before insert and an event on user deletion. This is the
  significant cost, and it is a real loss — the database can no longer guarantee something
  it easily could.
- Cross-module reads are two queries rather than one join, so listing a board with member
  names means fetching the board, then asking Users for the members. Slower, and the N+1
  trap is now something to watch for.
- Migrations are per module, so applying "the" migrations means iterating every
  `IModuleDatabaseMigrator`.
- The Identity tables had to be renamed out of their `AspNet*` defaults to sit sensibly in
  the schema.

## Related

- [concepts/modular-monolith.md](../concepts/modular-monolith.md)
- [concepts/ef-core-basics.md](../concepts/ef-core-basics.md)
