# Team Project Board

A real-time Kanban project management application for small teams.

Built as a personal learning project to explore modern .NET application architecture,
real-time collaboration, PostgreSQL and frontend state management. Every significant
decision is written down — see [Documentation](#documentation).

## Status

**Sprint 1 complete: the Users module.** Registration, login, JWT with rotating refresh
tokens, system roles and claim-based authorization, behind a modular monolith skeleton with
architecture tests and 64 passing tests.

| Sprint | Scope | Status |
|---|---|---|
| 1 | Identity and authentication | Done |
| 2 | Projects and membership | Next |
| 3 | Kanban boards, columns, tasks | |
| 4 | Task movement and concurrency | |
| 5 | Real-time collaboration (SignalR) | |
| 6 | React frontend | |
| 7 | Concurrency and polish | |
| 8 | Deployment | |

## Running locally

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker.

```bash
dotnet run --project src/backend/TeamProjectBoard.AppHost
```

That starts Postgres, pgAdmin and the API, and prints a URL for the Aspire dashboard —
where you will find logs, distributed traces (including every SQL statement) and metrics.

Nothing else to configure. The connection string and JWT signing key are injected by the
AppHost, so there is no local settings file to create.

A development admin is seeded: `admin@teamprojectboard.local` / `Admin123!`.

### Trying the API

Swagger is at `/swagger`. Or step through
[`api_requests/users.http`](src/backend/TeamProjectBoard.Host/api_requests/users.http),
which walks the whole flow — register, log in, refresh, replay a spent token, promote a
user — with the expected status code on each request explained.

### Tests

```bash
dotnet test src/backend/TeamProjectBoard.slnx
```

64 tests. The integration tests start a real Postgres via Testcontainers, so Docker must be
running. The architecture tests need nothing and run in about a second:

```bash
dotnet test src/backend/Common/Modules.Common.Tests.Architecture
```

## Features

The MVP:

- User accounts and project memberships
- Role-based permissions
- Team project management
- Kanban boards, columns and tasks
- Moving and reordering tasks
- Real-time board updates between connected users

## Architecture

A **modular monolith**: one deployable, with business domains kept in separate modules that
cannot reach into each other. Boundaries are enforced by project references, a Postgres
schema per module, and architecture tests that fail the build when one is crossed.

Inside each module, **Clean Architecture** keeps the domain free of infrastructure, and
**vertical slices** organise code by feature rather than by technical layer — everything
for "register a user" lives in one folder.

Modules:

| Module | Owns | Status |
|---|---|---|
| **Users** | Accounts, system roles, authentication | Built |
| **Projects** | Projects, membership, project roles | Sprint 2 |
| **Boards** | Boards, columns, tasks, ordering | Sprint 3–4 |
| **Collaboration** | SignalR hub, real-time broadcasts | Sprint 5 |

## Tech stack

| Area | Technology |
|---|---|
| Backend | ASP.NET Core 10, Minimal APIs |
| Architecture | Modular monolith + Clean Architecture + vertical slices |
| Database | PostgreSQL (EF Core 10, schema per module) |
| Auth | ASP.NET Core Identity, JWT with rotating refresh tokens |
| Real-time | SignalR (Sprint 5) |
| Frontend | React + TypeScript + DnD Kit (Sprint 6) |
| Local orchestration | .NET Aspire |
| Observability | OpenTelemetry, Serilog |
| Testing | xUnit, NSubstitute, Testcontainers, Respawn, NetArchTest |
| Deployment | Docker Compose (Sprint 8) |

## Documentation

Because this is a learning project, the reasoning is written down in two forms.

**[docs/concepts/](docs/concepts/)** explains *what things are and why they exist* — the
background you need to follow the code. Start with
**[Anatomy of a slice](docs/concepts/anatomy-of-a-slice.md)**, which traces one HTTP request
from the socket to the database row and back, touching every layer once.

Other notes cover the [modular monolith](docs/concepts/modular-monolith.md),
[Clean Architecture and slices](docs/concepts/clean-architecture-and-slices.md),
[CQRS and handlers](docs/concepts/cqrs-and-handlers.md),
[the Result pattern](docs/concepts/result-pattern.md),
[Minimal APIs](docs/concepts/minimal-apis-and-endpoints.md),
[dependency injection](docs/concepts/dependency-injection.md),
[EF Core](docs/concepts/ef-core-basics.md),
[ASP.NET Core Identity](docs/concepts/aspnet-core-identity.md),
[authentication and JWTs](docs/concepts/authentication-and-jwt.md),
[Aspire](docs/concepts/aspire.md),
[observability](docs/concepts/observability.md) and
[testing](docs/concepts/testing-strategy.md).

**[docs/adr/](docs/adr/)** records *why each choice was made over the alternatives*, and —
the part that matters — what each one costs. Twelve decisions so far.

**[docs/General.md](docs/General.md)** holds the original requirements and the sprint plan.

## Repository layout

```
docs/
  concepts/       background on the patterns and frameworks
  adr/            architecture decision records
  General.md      requirements and sprint plan
src/
  backend/        the API (see src/backend/CLAUDE.md for conventions)
    Common/       shared abstractions
    Users/        the Users module (see Users/README.md)
  frontend/       React app (Sprint 6)
```
