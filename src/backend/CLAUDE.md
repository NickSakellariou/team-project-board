# CLAUDE.md

## Overview

Team Project Board — a modular monolith Web API for a real-time Kanban board.

Decisions are documented as you go: see `docs/adr/` for decision records and
`docs/concepts/` for background on the frameworks and patterns. Start with
`docs/concepts/anatomy-of-a-slice.md`.

## Tech Stack

- .NET 10, ASP.NET Core Minimal APIs
- EF Core 10 with PostgreSQL (snake_case naming, schema per module)
- FluentValidation for request validation
- `Result<T>` for business errors (no exceptions for expected failures)
- ASP.NET Core Identity + JWT with rotating refresh tokens
- .NET Aspire for local orchestration (Docker Compose for deployment, Sprint 8)
- OpenTelemetry for tracing and metrics; Serilog for structured logging
- Swagger/OpenAPI

## Architecture

- Modular monolith with vertical slice architecture
- Clean Architecture: the Domain project references nothing
- Four projects per module: Domain, Features, Infrastructure, PublicApi
- CQRS with hand-written handlers (no MediatR, no mediator of any kind)
- Manual mapping — no AutoMapper

## Project Structure

```
src/backend/
  Common/
    Modules.Common.Domain/          Result<T>, Error, IHandler, IAuditableEntity
    Modules.Common.Application/     handler registration, validation helpers
    Modules.Common.API/             IApiEndpoint, ToProblem(), GlobalExceptionHandler, Swagger
    Modules.Common.Infrastructure/  JWT, IPolicyFactory, AuditableInterceptor, OpenTelemetry
    Modules.Common.Tests.Architecture/
    Modules.Common.Tests.Unit/
  Users/
    Modules.Users.Domain/
    Modules.Users.Features/
    Modules.Users.Infrastructure/
    Modules.Users.PublicApi/
    Modules.Users.Tests.Unit/
    Modules.Users.Tests.Integration/
  TeamProjectBoard.Host/            Program.cs, seeding, log catalogue, .http files
  TeamProjectBoard.Host.Tests.Unit/ the Host's tests that need no database
  TeamProjectBoard.AppHost/         Aspire orchestration
  TeamProjectBoard.ServiceDefaults/ health checks, service discovery, resilience
```

## Project reference rules

Enforced by `Modules.Common.Tests.Architecture` — a violation fails the build.

```
Domain         → Common.Domain only
Infrastructure → own Domain, Common.Infrastructure
Features       → own Infrastructure, Common.API, Common.Application,
                 other modules' PublicApi ONLY
PublicApi      → nothing at all (contracts only)
Host           → every module's Features, Common.*, ServiceDefaults
```

The PublicApi rule is the load-bearing one: every module may reference it, so anything
reachable from it is reachable by all of them.

## File naming

```
Features/<Area>/<UseCase>/<UseCase>.Endpoint.cs     request record + IApiEndpoint
Features/<Area>/<UseCase>/<UseCase>.Handler.cs      I<UseCase>Handler + sealed handler
Features/<Area>/<UseCase>/<UseCase>.Validator.cs    FluentValidation rules
Features/<Area>/Shared/Routes/RouteConsts.cs
Domain/Errors/<Module>Errors.cs                    every failure the module can return
Domain/Logging/<Module>Logs.cs                     every log event the module can emit
```

Several types share a slice file on purpose. MA0048 (file name must match type name) is
disabled for Features projects for exactly this reason.

## Code conventions

- Positional records for request and response DTOs
- File-scoped namespaces
- Primary constructors for dependency injection
- `sealed` on every implementation class
- `internal` by default; `public` only for contracts and DTOs
- XML `<summary>` on every public type and member in `Common/`

## Patterns we use

- `Result<T>` as every handler's return type
- `IHandler` marker interface for auto-registration
- `IApiEndpoint` for endpoint self-registration
- `RouteConsts` for centralised routes
- One FluentValidation validator per use case
- `IPolicyFactory` so each module declares its own authorization policies
- `IUserModuleApi`-style PublicApi contracts for cross-module calls
- EF Core `DbContext` directly in handlers
- One `[LoggerMessage]` log catalogue per project that logs, each event with a permanent
  event id — `CA1848` is an error, so a bare `logger.LogInformation(...)` will not compile.
  See `docs/log-event-ids.md` and the `adding-a-log` skill

## Patterns we do NOT use

- Repository pattern
- AutoMapper or any mapping library
- MediatR or any mediator
- Exceptions for expected business outcomes
- `[FromServices]` — the framework infers it since .NET 7
- `RequireRole(...)` — authorize on claims, not roles

## Commenting convention

Documentation is a deliverable here, not an afterthought.

- **XML docs on everything public in `Common/`** — these are the abstractions every module
  builds on, and they surface in IntelliSense at the point of use.
- **A one-line business rule on each handler**, in its `<remarks>` — the thing the code
  cannot say for itself.
- **`// Why:` on any line that looks arbitrary.** Framework quirks, non-obvious ordering,
  security-relevant choices.
- **Never suppress an analyzer without a reason on the line.** Either a
  `[SuppressMessage(..., Justification = "...")]`, a `#pragma` with a comment, or an entry
  in `.editorconfig` explaining the decision.
- Comment the *why*, never the *what*.

## Testing

- xUnit
- **NSubstitute** for substitutes (not Moq)
- Testcontainers + Respawn for integration tests against real Postgres
- NetArchTest for architecture tests
- Naming: `[Method]_[Scenario]_[ExpectedResult]`
- Verify a new architecture rule by breaking it once, then reverting

## DI registration

- One entry point per module: `Add<Module>Module(services, configuration)`
- `RegisterHandlersFromAssemblyContaining` — scans for `IHandler`, registers scoped
- `RegisterApiEndpointsFromAssemblyContaining` — scans for `IApiEndpoint`
- `AddValidatorsFromAssembly(..., includeInternalTypes: true)`
- Extension methods on `IServiceCollection` use the
  `Microsoft.Extensions.DependencyInjection` namespace so they appear without an extra using

## Database

- One database, one schema per module (`users`, later `projects`, `boards`)
- One `DbContext` and one `migration_history` table per module, inside its own schema
- **No foreign keys across schemas** — a cross-module reference is a plain id
- Migrations are generated, never hand-edited, and excluded from analyzer rules

```bash
dotnet ef migrations add <Name> \
  --project Users/Modules.Users.Infrastructure \
  --startup-project TeamProjectBoard.Host \
  --output-dir Database/Migrations
```

## Running

```bash
dotnet run --project src/backend/TeamProjectBoard.AppHost   # everything
dotnet build src/backend/TeamProjectBoard.slnx              # warnings are errors
dotnet test  src/backend/TeamProjectBoard.slnx              # needs Docker
```

The JWT signing key and seeded admin credentials are injected by the AppHost. They are
deliberately not in `appsettings.json`, which is deployed with the application.
