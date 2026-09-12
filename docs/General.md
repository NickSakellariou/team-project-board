# Team Project Board — Requirements and Plan

The original planning document: what we are building, in what order, and why.

Decisions that have since been made and implemented are recorded in [adr/](adr/); the
background behind them is in [concepts/](concepts/). Where this document and an ADR
disagree, the ADR is newer.

## The method

1. **Define requirements** (3–5 bullet points)
2. **Set up the foundation**
3. **Choose the architecture**
4. **Add architecture tests and observability**
5. **Build features one vertical slice at a time**

Define the business requirements first. What does the user need? What is the minimum viable
scope? Which features are v1 and which are later? Skipping this leads to building the wrong
thing, and fast tooling cannot save a misaligned product. Write a paragraph or two of intent
before any code.

Do not overengineer. Start with the minimum viable feature set; you can always add more.

---

## What it does

Track tasks, deadlines and Kanban columns for small team projects. Real-time collaboration
with drag and drop.

**What you will learn:** real-time collaboration with SignalR, drag-and-drop UI with DnD
Kit, Kanban board state management, PostgreSQL with .NET, and multi-user concurrency
handling.

---

## Step 1: Business requirements

Five requirements define the MVP:

- **Manage user accounts and project memberships with role-based permissions**
- **Create and manage team projects with project owners and members**
- **Create and manage Kanban boards, columns and tasks**
- **Move and reorder tasks across Kanban columns**
- **Provide real-time updates so all connected project members see board changes immediately**

That is it. Later: task comments, labels, due dates, notifications, file attachments,
activity history. A focused scope keeps the project manageable.

### Two levels of role

**System role** — who you are in the application as a whole:

- **Admin** — can manage users, and potentially all projects.
- **User** — a normal application user.

**Project role** — who you are within a specific project:

- **Owner** — can manage the project, its members and the board configuration.
- **Member** — can work with the board and tasks, but cannot manage the project.

These are separate because a project role varies per project: you can own one project and
be a plain member of another, so it cannot live on the user.

---

## Step 2: Architecture

### Why a modular monolith?

Team Project Board has four related but distinct domains: identity, project membership, the
Kanban board, and real-time collaboration. They share data — a board belongs to a project,
whose members are users — but each has its own rules and its own reasons to change.

**Why not a plain monolith?** Because the domains are distinct enough to benefit from clear
boundaries. Mixing authentication, project membership and board logic into flat controllers
and services produces a tangle within months, and untangling it later is far more work than
keeping it separate now.

**Why not microservices?** Because it is a side project with a single developer.
Microservices add distributed-system complexity — network calls, service discovery,
distributed transactions, a pipeline per service — with no benefit at this scale.

**A modular monolith** gives both: a single deployment with real module boundaries. If a
module ever needs to scale or deploy independently, the boundary it needs already exists.

See [ADR-0001](adr/0001-modular-monolith.md).

### Why Clean Architecture and vertical slices?

Inside each module:

- **Clean Architecture** keeps the domain free of infrastructure, which makes business
  rules testable without a database or a web server.
- **Vertical slices** organise code by feature, so everything for "move a task" is in one
  folder rather than spread across four.

See [ADR-0002](adr/0002-clean-architecture-vertical-slices.md).

---

## Step 3: Tech stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 10, Minimal APIs |
| Frontend | React + TypeScript + DnD Kit |
| Real-time | SignalR |
| Database | PostgreSQL |
| Local orchestration | .NET Aspire |
| Deployment | Docker Compose |

### Backend: ASP.NET Core 10 with Minimal APIs

The current release, with the best performance of any .NET version and built-in validation
support in Minimal APIs.

**Why Minimal APIs over controllers?** They fit vertical slices. Each endpoint is its own
class in its own feature folder, so a slice stays self-contained. Controllers accumulate
endpoints and grow into large classes whose actions share dependencies most of them do not
need. See [ADR-0005](adr/0005-minimal-apis.md).

**Also:** EF Core for data access, used directly in handlers rather than behind a repository
([ADR-0006](adr/0006-no-repository-pattern.md)); FluentValidation for request validation.

### Frontend: React + TypeScript + TailwindCSS

**React** — the largest ecosystem, and DnD Kit is the drag-and-drop library this board
needs.

**TypeScript** — type safety catches mistakes at compile time, which matters for board state
where tasks move between columns and order must stay consistent.

**TailwindCSS** — fast UI development without writing custom CSS.

*Alternatives:* Blazor to stay in .NET, Angular for something more opinionated, Vue for
something lighter.

### Database: PostgreSQL

**Why Postgres?** Free, fast, reliable, strong ACID transactions, and excellent EF Core
support via Npgsql. It handles everything a Kanban board needs: relational data
(project → board → column → task), JSON columns for flexible metadata, and full-text search
for finding tasks.

It also has real **schema** support, which is what lets each module own its own tables
inside one database — the boundary this architecture depends on. See
[ADR-0007](adr/0007-schema-per-module.md).

**Why not SQL Server?** Postgres is free in every environment. SQL Server needs licensing in
production or limits you to Express.

**Why not NoSQL?** The data is highly relational — a task belongs to a column, which belongs
to a board, which belongs to a project, whose members are users. Relational databases handle
this naturally, and task ordering within a column needs consistency guarantees.

### Local orchestration: .NET Aspire

One command starts Postgres and the API together, injects the connection string, and
provides a dashboard with logs, traces and metrics. No local settings file to create. See
[ADR-0010](adr/0010-aspire-for-local-orchestration.md).

### Deployment: Docker Compose, then Azure

**Compose** for the deployable artifact: one command runs the frontend, API and Postgres,
identically on any OS.

**Azure** for production: App Service or Container Apps for the API, Static Web Apps for the
frontend, Azure Database for PostgreSQL.

---

## Step 4: The foundation

1. **`Directory.Build.props`** — nullable reference types, analysis level, warnings as errors
2. **Static analyzers** — Meziantou, SonarAnalyzer, Roslynator, xunit.analyzers
3. **`.editorconfig`** — coding standards, with every suppression carrying a written reason
4. **`Directory.Packages.props`** — centralised NuGet versions
5. **Aspire** — an AppHost with Postgres, and ServiceDefaults with health checks
6. **GitHub Actions** — CI that builds, tests and creates Docker images *(not yet done)*

---

## Step 5: Protect the architecture

**Architecture tests, written first:**

- Domain must not depend on Infrastructure or Features
- A module must not reference another module's internal projects
- PublicApi must depend on nothing
- Handlers and endpoints must follow the naming convention the startup scans rely on

Verify each new rule by deliberately breaking it once and watching the test fail. A rule
that has never failed may not be capable of failing — a `const` violation, for instance,
slips past NetArchTest entirely because the compiler inlines it.

**Integration tests, written alongside features:**

- `WebApplicationFactory` + Testcontainers with a real Postgres
- Test the full flow through HTTP, as a client would
- Respawn to reset the database between tests

**Observability:**

- OpenTelemetry, exported to the Aspire dashboard locally
- Npgsql instrumentation so traces show each SQL statement
- Jaeger or Seq in production

See [ADR-0011](adr/0011-architecture-tests.md) and
[concepts/testing-strategy.md](concepts/testing-strategy.md).

---

## Step 6: Build features as vertical slices

### Sprint 1 — Users and authentication ✅

- Register, login, refresh token, get current user
- Get / update / delete user, change role (admin)
- System roles (`Admin` / `User`) and claim-based authorization
- JWT with rotating refresh tokens

### Sprint 2 — Project management

- Create / list / get / update / delete project
- Add, remove and list project members
- Project roles (`Owner` / `Member`) and project-level authorization

### Sprint 3 — Kanban board

- Create / get / update / delete board
- Create / update / delete / reorder columns
- Create / get / update / delete tasks

### Sprint 4 — Task management

- Move a task between columns; reorder within a column
- Assign a task to a project member
- Task status management
- Validate task ownership and project membership
- Optimistic concurrency

### Sprint 5 — Real-time collaboration

- A SignalR board hub, with authenticated users joined to a project board
- Broadcast task creation, updates, deletion, movement and reordering
- Broadcast column changes
- Handle client reconnection

### Sprint 6 — React frontend

- Authentication pages
- Project list and creation; member management
- Kanban board UI with DnD Kit
- Task creation, editing and assignment
- API and SignalR integration

### Sprint 7 — Concurrency and polish

- Simultaneous task updates and stale versions
- Resolve or reject conflicting updates
- Authorization edge cases
- Loading, error and empty states

### Sprint 8 — Deployment

- Containerize; Docker Compose for the full stack
- Production configuration and database
- Health checks, logging and monitoring

Each sprint produces a working, testable increment.

---

## Modules

### 1. Users ✅

Users and system-level roles. Registration, login, user management, authentication,
`Admin` / `User`. Depends on nothing. See [src/backend/Users/README.md](../src/backend/Users/README.md).

*(Called the "Identity Module" in the original plan; renamed to avoid colliding with
`Microsoft.AspNetCore.Identity` — see [ADR-0012](adr/0012-module-and-project-naming.md).)*

### 2. Projects

Projects and membership. Project roles (`Owner` / `Member`) and project-level
authorization. Will reference `Modules.Users.PublicApi` and nothing else from Users.

### 3. Boards

The Kanban functionality: boards, columns, tasks, ordering, and moving tasks between
columns.

### 4. Collaboration

Real-time communication: the SignalR hub, board connections, and broadcasting task and
column changes.

---

## Step 7: Deploy

**Local development:** `dotnet run --project src/backend/TeamProjectBoard.AppHost`.

**Production, to Azure:**

1. Push Docker images to Azure Container Registry
2. Deploy the API to App Service or Container Apps
3. Deploy the React frontend to Static Web Apps
4. Provision Azure Database for PostgreSQL
5. Configure environment variables and connection strings — including the JWT signing key,
   which must come from a secret store and never from committed configuration
6. GitHub Actions for deployment on push to main
