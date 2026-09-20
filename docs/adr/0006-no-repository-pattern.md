# 0006. EF Core `DbContext` directly in handlers, no repository pattern

**Status:** Accepted
**Date:** 2026-09-13

## Context

Handlers need data. The conventional layered answer is a repository per aggregate —
`IUserRepository` with `GetByIdAsync`, `AddAsync`, `UpdateAsync` — injected into the
handler, hiding EF Core behind an interface.

## Options considered

**Generic repository.** `IRepository<T>` with the usual five methods. Every entity gets the
same API whether or not it fits. Queries that do not match the five methods either leak
`IQueryable` out of the repository — which defeats the abstraction, since callers can then
build any query — or get bolted on as one-off methods until the interface is thirty methods
wide.

**Repository per aggregate.** `IUserRepository` with methods shaped to actual use cases.
Better fitted, and a real abstraction over persistence. The cost is a file per aggregate
that mostly forwards to `DbContext`, plus a method added for every new query.

**`DbContext` directly.** Handlers query EF Core.

## Decision

`DbContext` directly. `UserModuleApi` queries `dbContext.Users`; handlers use
`UserManager<User>`, which is itself Identity's repository over the same context.

## Consequences

**Easier**

- No layer of forwarding methods. Adding a query means writing it, not adding an interface
  method and an implementation first.
- The full power of EF Core at each call site: `AsNoTracking`, projection in SQL,
  `ExecuteUpdateAsync`, `Include`. A generic repository hides most of these, and hiding
  `AsNoTracking` costs real performance on read paths.
- `DbContext` is already both a unit of work and a repository. Wrapping it duplicates
  abstractions the library provides.

**Harder**

- **Handlers cannot be unit-tested without a database.** This is the real cost, and it is
  why the integration tests exist and use Testcontainers. `LoginUserHandler` is testable in
  isolation only because it depends on `IAuthenticationService`, not on a repository.
- Query logic lives in handlers, so two slices needing a similar query write it twice —
  consistent with [ADR-0002](0002-clean-architecture-vertical-slices.md), and still
  duplication.
- Swapping EF Core for something else would touch every handler. Accepted: that is a
  hypothetical, and the abstraction to prevent it would be paid for daily.
- Nothing structurally prevents a handler from writing an inefficient query. Discipline and
  the trace waterfall are the mitigation.

## Related

- [concepts/ef-core-basics.md](../concepts/ef-core-basics.md)
- [concepts/testing-strategy.md](../concepts/testing-strategy.md)
