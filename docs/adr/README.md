# Architecture Decision Records

An ADR captures one decision: the problem, the options weighed, what was chosen, and what
it costs. Written at the time the decision is made, and never rewritten afterwards — a
record of what was known *then*. If a decision is reversed later, the old ADR is marked
superseded and a new one explains why. Editing history to look wiser defeats the purpose.

These are short. The background they rest on is in [../concepts/](../concepts/), and each
ADR links to the relevant note.

## Index

| # | Decision | Status |
|---|---|---|
| [0001](0001-modular-monolith.md) | Modular monolith over microservices and layered monolith | Accepted |
| [0002](0002-clean-architecture-vertical-slices.md) | Clean Architecture with vertical slices | Accepted |
| [0003](0003-cqrs-hand-written-handlers.md) | CQRS with hand-written handlers, no MediatR | Accepted |
| [0004](0004-result-pattern.md) | `Result<T>` for business errors instead of exceptions | Accepted |
| [0005](0005-minimal-apis.md) | Minimal APIs with self-registering endpoints | Accepted |
| [0006](0006-no-repository-pattern.md) | EF Core `DbContext` directly in handlers | Accepted |
| [0007](0007-schema-per-module.md) | One database, one schema per module | Accepted |
| [0008](0008-identity-and-jwt.md) | ASP.NET Core Identity with JWT and rotating refresh tokens | Accepted |
| [0009](0009-claim-based-authorization.md) | Claim-based policies contributed per module | Accepted |
| [0010](0010-aspire-for-local-orchestration.md) | .NET Aspire for local orchestration | Accepted |
| [0011](0011-architecture-tests.md) | NetArchTest to enforce module boundaries | Accepted |

## Template

```markdown
# NNNN. Title

**Status:** Accepted | Superseded by NNNN
**Date:** YYYY-MM-DD

## Context
What problem, and what constrains the answer.

## Options considered
Each with its real trade-off, not a strawman.

## Decision
What we chose.

## Consequences
What this makes easier, and — the part that matters — what it makes harder.
```

The consequences section is the one worth writing carefully. Anyone can list why a choice
is good; the useful record is what you gave up, so that when the cost shows up in six
months it is a known cost rather than a surprise.
