# The Modular Monolith

## The three options

**A plain monolith** is one deployable with no internal boundaries. Fast to start, and it
degrades predictably: with nothing preventing it, every class ends up able to reach every
other, and after a year you cannot change anything without reading everything.

**Microservices** are many deployables with boundaries enforced by the network. The
boundaries are real — you cannot accidentally call another service's database — but you
pay for them with distributed systems problems: network failure between every call,
service discovery, distributed transactions, tracing across processes, and a deployment
pipeline per service.

**A modular monolith** is one deployable with boundaries enforced by tooling. One process,
one database, one deployment — but modules that cannot reach into each other.

## Why this project chose the third

The scale argument is decisive. This is a side project with one developer and four planned
modules. Microservices would add every distributed-systems cost and return nothing: there
is no team to decouple, no component with different scaling needs, no independent release
cadence to protect.

But the domains here *are* genuinely distinct — identity, project membership, the Kanban
board, real-time collaboration — and mixing them into flat controllers and services is the
failure mode a plain monolith drifts into.

The modular monolith takes the boundaries and skips the network. And crucially: if one
module ever needs to become a service, the boundary it needs already exists. That is the
migration path a plain monolith does not have.

## What "boundary" means when it is all one process

This is the part that is easy to say and hard to mean. Nothing in a single process
physically stops one class calling another. The boundary has to be constructed, and it is
constructed in four layers:

### 1. Project references

`Modules.Users.Features` references its own Infrastructure, the Common projects, and other
modules' `PublicApi` — nothing else. A module physically cannot see another's internals
because the assembly is not referenced.

### 2. The PublicApi project

Each module exposes one project containing only contracts:

```
Modules.Users.PublicApi/
    IUserModuleApi.cs
    Contracts/UserSummary.cs
```

It references **nothing** — not even its own module's Domain. That is the load-bearing
constraint, because every other module may reference this project, so anything reachable
from it is transitively reachable by all of them. Let one contract expose a domain entity
and the boundary is gone, quietly, with everything still compiling.

`ModuleBoundaryTests.UsersPublicApi_ShouldNotDependOn_TheRestOfItsModule` is the test that
catches it.

### 3. Database schemas

Each module owns a Postgres schema and its own `DbContext`. The Users module's tables are
in `users`; Boards' will be in `boards`. Nothing is in `public`.

This is the boundary that matters most in practice, because sharing a database is the
usual way modular monoliths actually die: someone writes one convenient join across two
modules' tables, and now those modules cannot change their storage independently ever
again. A separate `DbContext` cannot even see the other schema's tables.

**No foreign keys across schemas.** When Projects stores a `UserId`, it is a plain string
with no FK. That loses referential integrity — the database will not stop a membership row
pointing at a deleted user — and buys the ability for either module to change storage
alone. Compensating for it is the module's job, via `IUserModuleApi.UserExistsAsync`.

### 4. Architecture tests

Rules that live only in documentation are rules that erode. `Modules.Common.Tests.Architecture`
turns each one into a build failure. They run in milliseconds and need no database — the
cheapest tests in the solution and, for this style, among the most valuable.

## Module vs bounded context

Loosely, a **bounded context** (from Domain-Driven Design) is a boundary within which a
term has one consistent meaning. "User" means one thing to Identity — credentials, roles,
lockout — and a different thing to Boards, where it is essentially just a name and an
avatar to draw on a card.

That difference is the point. The wrong instinct is one shared `User` class serving both,
which ends up carrying every field either context needs and belonging properly to neither.
The right instinct is that each context keeps its own view, related by an id.

This is exactly why `IUserModuleApi` returns `UserSummary` — id, email, display name — and
not `User`. The Boards module gets what a board needs, not the identity system's model.

## How modules talk

**Synchronously, through PublicApi.** Projects needs to know a user exists before adding
them to a project. It injects `IUserModuleApi` and calls it — an ordinary in-process method
call, no serialization, no network, and it participates in the caller's request scope.

**Asynchronously, through events.** For "something happened, whoever cares should react" —
a user is deleted and their project memberships should go. Not built yet; it becomes
worthwhile once a second module exists.

The asymmetry is deliberate. Synchronous calls are simple and create a runtime dependency:
if Users is broken, Projects is broken. Events are looser but eventually consistent and
harder to debug. Start with the simple one; reach for events when the coupling actually
hurts.

## The modules in this project

| Module | Owns | Status |
|---|---|---|
| **Users** | Accounts, system roles, authentication | Built |
| **Projects** | Projects, membership, project roles (Owner/Member) | Sprint 2 |
| **Boards** | Boards, columns, tasks, ordering | Sprint 3–4 |
| **Collaboration** | The SignalR hub and real-time broadcasts | Sprint 5 |

The intended dependency graph:

```
Collaboration ──→ Boards ──→ Projects ──→ Users
                     └──────────┴───────────┘
                         (all via PublicApi)
```

Users depends on nothing. That is why it was built first.

## What makes a module extractable later

If Users ever became a separate service, what would have to change?

- `IUserModuleApi` — the implementation becomes an HTTP client. **Callers do not change.**
- The `users` schema moves to its own database. Nothing joins to it, so nothing breaks.
- Every caller must now handle network failure and latency, which in-process calls never had.
- Anything that relied on one transaction spanning Users and another module breaks.

The first two are nearly free *because* of the boundaries. The last two are the real cost
of distribution, and they are why we are not paying it yet.

## When this is the wrong choice

Worth being honest: for a genuinely small application — one domain, a handful of entities,
no growth expected — this structure is overhead with no return. Four projects and an
architecture test suite to store a user is not obviously sensible.

The judgement is about where the code is going, not where it is. See
[ADR-0001](../adr/0001-modular-monolith.md).

## Related

- [clean-architecture-and-slices.md](clean-architecture-and-slices.md) — the structure inside a module
- [testing-strategy.md](testing-strategy.md) — how the architecture tests work
- [ADR-0007](../adr/0007-schema-per-module.md) — the database decision
