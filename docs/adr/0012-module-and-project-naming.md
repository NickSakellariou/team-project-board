# 0012. Module named `Users`; projects suffixed `.Features`

**Status:** Accepted
**Date:** 2026-09-13

## Context

The three planning documents in this repository disagreed with each other and with the two
reference implementations in `C:\Projects`:

- `docs/General.md` called the first module the **Identity Module**.
- `src/backend/CLAUDE.md` specified projects named `Modules.[Name].Core`.
- Both `modular-monolith-template-main` and `InvoiceMaker` use `Modules.Users.*` and
  `.Features`.
- `CLAUDE.md` also specified Moq for mocking; both reference repos use NSubstitute.

Naming is not important in itself, and inconsistent naming is: two names for one thing in a
convention document means the convention is not one.

## Options considered

**`Modules.Identity.*`.** Matches `docs/General.md`. Collides visually with
`Microsoft.AspNetCore.Identity` in every using block and every discussion — "the Identity
module" and "Identity" would mean two different things in the same sentence.

**`Modules.Users.*`.** Matches both reference repos. Slightly narrower in meaning:
authentication is arguably not "users". In practice this module owns the user entity, so
the name fits.

**`.Core` vs `.Features`.** "Core" says nothing about what a project contains — every layer
thinks it is the core. "Features" names what is inside: the vertical slices.

## Decision

- Module: **`Users`**. `docs/General.md` was updated to match.
- Project suffix: **`.Features`**. `CLAUDE.md` was updated to match.
- Mocking: **NSubstitute**. `CLAUDE.md` was updated to match.

So each module is `Modules.<Name>.{Domain,Features,Infrastructure,PublicApi}`, plus
`Modules.<Name>.Tests.{Unit,Integration}`.

## Consequences

**Easier**

- One name per concept across code, documentation and both reference repos.
- No ambiguity between our module and the framework in conversation or in `using` lines.
- Patterns can be copied from the reference repos without translating names.

**Harder**

- The existing planning documents had to be corrected, and anything written against the old
  names is now wrong.
- "Users" is a slightly imprecise name for a module that is mostly about authentication. If
  it ever grows profiles, preferences and notification settings, the name will fit better
  than it does today.

## Related

- [ADR-0002](0002-clean-architecture-vertical-slices.md)
