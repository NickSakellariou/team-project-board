# Concepts

Background on the ideas and frameworks this codebase leans on. These explain *what a thing
is and why it exists*; the [ADRs](../adr/) record *why we chose it over the alternatives*.

Each note follows the same shape:

1. **What it is**
2. **Why it exists** — what goes wrong without it
3. **How we use it here** — pointing at real files
4. **What we deliberately skipped**

Written for someone comfortable with C# who has not built this kind of application before.

## Reading order

**Start here.** [anatomy-of-a-slice.md](anatomy-of-a-slice.md) traces one HTTP request —
`POST /api/users/register` — from the socket to the database row and back. Every layer
appears once, in the order the request meets it. Read it with the repo open; everything
else is detail on one of its steps.

Then, depending on what you are doing:

### The architecture

| Note | Covers |
|---|---|
| [modular-monolith.md](modular-monolith.md) | Modules, boundaries, what makes one extractable later |
| [clean-architecture-and-slices.md](clean-architecture-and-slices.md) | The dependency rule; why slices instead of layers |
| [cqrs-and-handlers.md](cqrs-and-handlers.md) | One class per use case; why no MediatR |
| [result-pattern.md](result-pattern.md) | Errors as return values instead of exceptions |

### The frameworks

| Note | Covers |
|---|---|
| [minimal-apis-and-endpoints.md](minimal-apis-and-endpoints.md) | Endpoints without controllers; parameter binding |
| [dependency-injection.md](dependency-injection.md) | Lifetimes, the captive-dependency trap, `IConfigureOptions` |
| [ef-core-basics.md](ef-core-basics.md) | Change tracking, migrations, schemas, interceptors |

### Security

| Note | Covers |
|---|---|
| [aspnet-core-identity.md](aspnet-core-identity.md) | What `IdentityUser` gives you; password hashing; the seven tables |
| [authentication-and-jwt.md](authentication-and-jwt.md) | JWT anatomy; why tokens cannot be revoked; refresh rotation |

### Operations

| Note | Covers |
|---|---|
| [aspire.md](aspire.md) | What the AppHost does; service discovery; connection-string injection |
| [observability.md](observability.md) | Logs vs traces vs metrics; OpenTelemetry |
| [../log-event-ids.md](../log-event-ids.md) | The log catalogues; which ids each layer owns; what every id means |
| [testing-strategy.md](testing-strategy.md) | Which suite catches which bug; Testcontainers; Respawn |

## If you are looking for something specific

- *Why does the domain project reference nothing?* → [clean-architecture-and-slices.md](clean-architecture-and-slices.md)
- *What is `IdentityUser` and why do I need one?* → [aspnet-core-identity.md](aspnet-core-identity.md)
- *Why can't I just log out a stolen token?* → [authentication-and-jwt.md](authentication-and-jwt.md)
- *Why is `DbContext` scoped and the interceptor a singleton?* → [dependency-injection.md](dependency-injection.md)
- *Why is there no `UserService`?* → [cqrs-and-handlers.md](cqrs-and-handlers.md)
- *Why does my new endpoint 404?* → [minimal-apis-and-endpoints.md](minimal-apis-and-endpoints.md) (check the naming convention)
- *Why a real Postgres in tests?* → [testing-strategy.md](testing-strategy.md)
- *Why won't `logger.LogInformation(...)` compile?* → [../log-event-ids.md](../log-event-ids.md)
- *Where does the connection string come from?* → [aspire.md](aspire.md)
