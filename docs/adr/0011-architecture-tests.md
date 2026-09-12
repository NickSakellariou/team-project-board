# 0011. NetArchTest to enforce module boundaries

**Status:** Accepted
**Date:** 2026-09-13

## Context

[ADR-0001](0001-modular-monolith.md) chose boundaries that are constructed rather than
physical. Project references enforce much of it, but not everything: naming conventions the
startup scans depend on, whether handlers are sealed and internal, and whether the PublicApi
project has stayed dependency-free.

Architecture erodes one reasonable-looking shortcut at a time. Adding a `ProjectReference`
takes ten seconds and always feels justified in the moment.

## Options considered

**Documentation and discipline.** Write the rules down and follow them. Free, and rules that
live only in documentation are rules that erode — especially on a solo project where nobody
reviews the pull request.

**Code review.** Effective with a team. There is no team.

**Architecture tests.** Assert the rules as unit tests over the compiled assemblies. They
run in about a second, need no database, and fail the build when a boundary is crossed.

## Decision

`Modules.Common.Tests.Architecture`, using NetArchTest. Twelve tests covering module
boundaries (`ModuleBoundaryTests`) and the conventions the startup scans rely on
(`ConventionTests`).

Each project exposes an `AssemblyReference` class so tests name assemblies in a way the
compiler checks, rather than by string.

## Consequences

**Easier**

- A violation fails the build with the offending type named, at the moment it is introduced.
- Rules the compiler cannot express — naming, sealing, visibility — are enforced anyway,
  which is what makes the assembly-scan registration in
  [ADR-0003](0003-cqrs-hand-written-handlers.md) and [ADR-0005](0005-minimal-apis.md) safe.
- Rules stay accurate, because a stale rule fails rather than sitting wrong in a document.
- They run in about a second with no infrastructure, so there is no reason to skip them.

**Harder**

- **They do not catch everything.** NetArchTest reads type references in IL. It does not see
  `const` values, which C# inlines at compile time — this was found by deliberately breaking
  a boundary with a `const` and watching the test pass. It also cannot see a violation made
  by reflection, or by raw SQL naming another schema's table. A strong guard, not a complete
  one.
- The test project must reference every module's assemblies, so it is the one project
  allowed to see all of them.
- The rules need maintaining. Adding a module means extending them, and a rule someone
  forgets to add is a boundary nobody is checking.
- A `[Fact]` asserting on a string list of namespaces is not self-evidently correct. Hence
  the requirement to verify each rule by breaking it once.

## Related

- [concepts/testing-strategy.md](../concepts/testing-strategy.md)
- [concepts/modular-monolith.md](../concepts/modular-monolith.md)
