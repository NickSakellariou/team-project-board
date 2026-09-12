# 0002. Clean Architecture with vertical slices

**Status:** Accepted
**Date:** 2026-09-13

## Context

[ADR-0001](0001-modular-monolith.md) settled the boundaries *between* modules. This decides
the structure *inside* one.

Two things need to be true. Business rules should not depend on frameworks, so they can be
read and tested without a database or a web server. And working on a feature should not
mean editing files scattered across the codebase.

## Options considered

**Layers only** (Controllers / Services / Repositories / Models). Familiar, and every
feature is spread across four folders that each mix a dozen unrelated features. Service
classes accumulate until they are the only place anyone looks.

**Slices only, no layers.** Everything for a feature in one folder, data access included.
Very fast to write. Domain rules end up duplicated across slices, and the duplicates drift,
so the same rule is enforced two different ways.

**Both.** Clean Architecture for the layer *dependencies*, vertical slices for the file
*organisation*.

## Decision

Both. Four projects per module — `Domain` → `Infrastructure` → `Features` → `PublicApi` —
with the Features project organised as one folder per use case.

The project suffix is `.Features`, not `.Core`: "Core" says nothing about what is inside.

## Consequences

**Easier**

- The Domain project references nothing and can be tested with no infrastructure at all.
- Working on a feature means opening one folder. Deleting a feature means deleting a folder,
  with nothing left behind — the most reliable sign the grouping is right.
- Changing one feature cannot break another, because they share no code.
- Infrastructure is replaceable: the domain names a capability, the implementation is chosen
  at startup.

**Harder**

- **Slices duplicate.** `GetUserById.Handler` and `GetCurrentUser.Handler` are nearly
  identical and stay that way. This is the central cost of the style. It is accepted because
  shared code is coupling, and the moment one of them must change it changes alone. The line
  to hold: duplicate *use-case logic* freely, never *domain rules*.
- Four projects per module is more ceremony than a small application needs.
- Where shared things live has to be decided deliberately — `Shared/Routes`,
  `Shared/UserResponse`, `Domain/Errors` — and each is a small exception to the rule.

## Related

- [concepts/clean-architecture-and-slices.md](../concepts/clean-architecture-and-slices.md)
- [concepts/anatomy-of-a-slice.md](../concepts/anatomy-of-a-slice.md)
