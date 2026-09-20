# 0001. Modular monolith over microservices and layered monolith

**Status:** Accepted
**Date:** 2026-09-13

## Context

Team Project Board has four distinct domains: identity, project membership, the Kanban
board, and real-time collaboration. They share data — a board belongs to a project, a
project has members who are users — but each has its own rules and its own reasons to
change.

It is a side project with one developer. There is no team to decouple, no component with
different scaling needs, and no independent release cadence to protect.

## Options considered

**Layered monolith.** One deployable, organised by technical layer, no internal boundaries.
Fastest to start. Degrades predictably: with nothing preventing it, every class ends up
able to reach every other, and the domains tangle. Recovering boundaries later means
untangling code that had no reason to stay separate.

**Microservices.** Boundaries enforced by the network, which makes them real — you cannot
accidentally query another service's database. But every call becomes a potential network
failure, and you acquire service discovery, distributed transactions, cross-process tracing
and a deployment pipeline per service. At one developer and four modules, all cost and no
return.

**Modular monolith.** One deployable, boundaries enforced by project references, database
schemas and architecture tests. Boundaries are constructed rather than physical, so they
require discipline — but tooling can supply most of that discipline.

## Decision

Modular monolith. One process, one database, one deployment; four modules that cannot reach
into each other.

## Consequences

**Easier**

- One deployment, one connection string, one log stream.
- Real ACID transactions across the application; no distributed-transaction problem.
- In-process calls between modules: no serialization, no network failure, no latency.
- If a module ever needs to become a service, the boundary it needs already exists.

**Harder**

- The boundaries are not physical. Nothing in the runtime stops a violation, so they must be
  enforced by project references, schemas and [ADR-0011](0011-architecture-tests.md). That
  enforcement is work, and it can be undone by anyone with commit access.
- No independent scaling or deployment. One module's bug can take the whole application down.
- More projects than a layered monolith needs, which for a small application is real
  overhead with no return. Justified by where this codebase is going, not where it is.
- Cross-module queries are deliberately awkward: joining users to projects means an API call
  rather than a SQL join. See [ADR-0007](0007-schema-per-module.md).

## Related

- [concepts/modular-monolith.md](../concepts/modular-monolith.md)
