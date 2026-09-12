# 0003. CQRS with hand-written handlers, no MediatR

**Status:** Accepted
**Date:** 2026-09-13

## Context

Each vertical slice needs somewhere for its logic to live — reachable from an endpoint, and
substitutable in a test.

Commands (register a user, move a task) and queries (get a user, list a board) have
different needs: commands want change tracking and transactions, queries want
`AsNoTracking` and projection straight to a DTO. Forcing both through one abstraction gives
each the other's compromises.

## Options considered

**A service class per entity.** `UserService` with eight methods. Its constructor needs
every dependency any method needs, so registering a user pulls in an email sender it never
touches. Editing one method means checking eight callers. Service classes only grow.

**MediatR.** The .NET default. `mediator.Send(new RegisterUserCommand(...))` finds the
handler. Its real value is the pipeline — validation, logging, transactions applied once to
every handler. Its cost is indirection: `Send` is not navigable, stack traces gain library
frames, and a missing handler becomes a runtime failure rather than a compile error.

**Hand-written handlers, called directly.** One class per use case with its own narrow
interface, resolved from DI and called by name.

## Decision

Hand-written handlers. One `I<UseCase>Handler : IHandler` and one sealed implementation per
slice, discovered at startup by an assembly scan and registered scoped.

## Consequences

**Easier**

- Ctrl-click on `handler.HandleAsync` goes to the code that runs.
- Each handler declares only the dependencies it actually uses.
- Each has exactly one caller, so changing it is safe.
- Stack traces contain our frames only.

**Harder**

- **No pipeline.** Cross-cutting behaviour is repeated. Today that is validation, written
  explicitly in each endpoint — four lines, and readable. If several modules later want the
  same transaction-and-audit wrapper on every command, this is the decision to revisit,
  either with MediatR or a small decorator of our own.
- **The assembly scan is not greppable.** Nothing in the source says `RegisterUserHandler`
  is registered. `ConventionTests` compensates by asserting the naming, sealing and
  visibility the scan depends on, so a mistake fails the build rather than 404ing at
  runtime.
- `IHandler` is an empty marker interface, which analyzers object to (CA1040, suppressed
  with a stated reason).

## Related

- [concepts/cqrs-and-handlers.md](../concepts/cqrs-and-handlers.md)
