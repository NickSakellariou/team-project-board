# 0005. Minimal APIs with self-registering endpoints

**Status:** Accepted
**Date:** 2026-09-13

## Context

The API needs HTTP endpoints. [ADR-0002](0002-clean-architecture-vertical-slices.md) put
everything for a feature in one folder, so whatever handles HTTP has to fit that: one
endpoint per file, next to its handler and validator.

## Options considered

**Controllers.** The long-standing default. A controller is a class that *accumulates*
endpoints, which works directly against vertical slices — `UsersController` would hold all
eight of this module's operations. Its constructor declares what any action needs, so each
action carries dependencies it never uses.

**Minimal APIs mapped in `Program.cs`.** Each endpoint is a delegate. Simple, and
`Program.cs` becomes fifty `app.MapGet(...)` lines that every feature branch edits and
therefore every merge conflicts on.

**Minimal APIs with a self-registration interface.** Each endpoint is a class implementing
`IApiEndpoint`, found by an assembly scan at startup.

## Decision

Minimal APIs with `IApiEndpoint`. Endpoints register their own routes;
`RegisterApiEndpointsFromAssemblyContaining` finds them and `MapApiEndpoints` maps them.

No `[FromServices]` — since .NET 7 the framework infers it for registered service types.

## Consequences

**Easier**

- One endpoint per file, in its slice folder, with its handler and validator.
- Adding a route means adding a file. `Program.cs` never changes, so it is never a merge
  conflict.
- Each endpoint declares only what it needs, as method parameters.
- Faster than controllers — no action-descriptor lookup, no filter pipeline. A footnote at
  this scale, not a reason.

**Harder**

- **The registration is not greppable.** Nothing says `RegisterUserEndpoint` is registered;
  an endpoint that breaks the naming convention silently does not exist. `ConventionTests`
  turns that into a build failure.
- Less familiar than controllers, and most tutorials assume controllers.
- No built-in action filters. Cross-cutting concerns are written explicitly — currently just
  validation, four lines per endpoint.
- OpenAPI metadata is manual: `.Produces<T>()` and `.ProducesProblem(...)` per endpoint,
  where controllers can infer some of it from the return type.

## Related

- [concepts/minimal-apis-and-endpoints.md](../concepts/minimal-apis-and-endpoints.md)
