# .NET Aspire

## What it is

A local orchestrator. `TeamProjectBoard.AppHost` is a small console program whose only job
is to start everything the application needs and connect it together:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("team-project-board-postgres-data")
    .WithPgAdmin();

var database = postgres.AddDatabase("teamprojectboard");

builder.AddProject<Projects.TeamProjectBoard_Host>("api")
    .WithReference(database)
    .WaitFor(database);

await builder.Build().RunAsync();
```

Running it starts a Postgres container, waits for it to be ready, starts pgAdmin, starts
the API with a connection string pointing at the database, and opens a dashboard showing
logs, traces and metrics from all of it.

**None of this ships.** In production the orchestrator is Docker Compose, Kubernetes or
Azure Container Apps. Aspire is a development-time tool.

## The problem it solves

Without it, the README says something like:

> 1. Install Postgres, or run `docker run -d -p 5432:5432 -e POSTGRES_PASSWORD=...`
> 2. Create a database called `teamprojectboard`
> 3. Copy `appsettings.Development.json.example` and fill in the connection string
> 4. Set the JWT signing key
> 5. Run `dotnet run --project TeamProjectBoard.Host`

Five steps, each a place to get it wrong, and a set of instructions that drifts out of date
because nothing checks them. With Aspire it is one command, and the setup *is* code — it
cannot silently disagree with reality.

## Connection strings, and one deliberate deviation

`WithReference(database)` injects an environment variable into the API:

```
ConnectionStrings__teamprojectboard=Host=localhost;Port=54321;Database=teamprojectboard;...
```

The double underscore is .NET's convention for nesting in an environment variable name, so
this binds to the configuration key `ConnectionStrings:teamprojectboard` — which is what
`configuration.GetConnectionString("teamprojectboard")` reads.

So the connection string is never written in `appsettings.json` and never committed. The
port is assigned at run time, which is why nothing in the source names one.

**The deviation.** The reference template does:

```csharp
var postgres = builder.AddPostgres("Postgres");
builder.AddProject<...>(...).WithReference(postgres);   // references the SERVER
```

Referencing the server injects a connection string with **no database name**, so EF
connects with nothing selected and fails at the first query. `AddDatabase(...)` declares a
database inside the server, Aspire creates it if missing, and referencing *that* produces a
usable connection string. Hence the extra line here.

## `WaitFor`

```csharp
.WaitFor(database)
```

Holds the API until Postgres reports healthy. Without it the API starts first, tries to
migrate against a container that is not listening yet, and crashes.

## The signing key lives in the AppHost

```csharp
const string DevelopmentSigningKey = "development-only-signing-key-not-for-any-real-environment";

builder.AddProject<Projects.TeamProjectBoard_Host>("api")
    .WithEnvironment("AuthConfiguration__Key", DevelopmentSigningKey)
```

This is a deliberate placement worth understanding.

The key must exist or the app will not start — `AddJwtAuthentication` validates it on
startup. But it must not be in `appsettings.json`, because appsettings is *deployed with
the application*, so a key committed there is a key that reaches production unless someone
remembers to override it.

`appsettings.Development.json` is not the answer either: it is gitignored, so a fresh clone
would not run.

The AppHost is a development-only program that is never deployed at all. A value there
cannot reach production by accident. In production the same variable comes from a secret
store.

The same reasoning covers the seeded admin credentials.

## The dashboard

Aspire's dashboard (its URL is printed at startup) shows, for every resource:

- **Console logs** — the API's and the container's, in one place.
- **Structured logs** — Serilog's output with its properties queryable, not flattened to text.
- **Traces** — every request as a waterfall of spans, including each SQL statement, because
  `AddCoreInfrastructure` registers Npgsql instrumentation.
- **Metrics** — request rates, durations, GC.

The traces are the part that changes how you debug. A slow endpoint stops being "this took
800ms" and becomes "this took 800ms, of which 780ms was one query" — which is usually the
whole answer.

The plumbing behind it: the app exports OpenTelemetry over OTLP, and the dashboard is the
collector. Same protocol you would point at Jaeger or Grafana in production, so the local
setup is not a special case. See [observability.md](observability.md).

## ServiceDefaults

`TeamProjectBoard.ServiceDefaults` is referenced by every service, and holds what they
should all share: health checks, service discovery, HTTP resilience.

With one service this looks like indirection for its own sake, and honestly it is. It is
kept because it is where a SignalR service or a background worker would plug in later, and
because it is the shape Aspire tooling expects.

**One thing is deliberately absent: OpenTelemetry.** The stock Aspire template configures
it here. Ours does not, because our modules need tracing to include their own activity
sources and Npgsql, so it all lives in one place — `AddCoreInfrastructure`. Configuring it
in both would register the instrumentation twice and duplicate every span. The comment at
the top of `ServiceDefaultsExtensions` says so, because the absence is otherwise the kind
of thing someone helpfully "fixes".

### Service discovery

Lets code say `http://api` instead of `http://localhost:5193`. Aspire resolves the real
address at run time, so ports never appear in source. Unused today with one service;
relevant the moment there are two.

### Health endpoints

Two, because they answer different questions:

- **`/alive`** — is the process running? If it fails, restart the container.
- **`/health`** — can it serve traffic, dependencies included? If it fails, stop routing
  here but do **not** restart, because a restart will not fix a database that is down.

Both are Development-only. They report dependency names and failure reasons, which is
reconnaissance for an attacker. Exposing them in production needs authentication or a port
only the orchestrator can reach.

## What Aspire is not

- **Not a production runtime.** It can generate a manifest for deployment tooling, but it
  does not run your production system.
- **Not a replacement for Docker Compose.** Compose is still the plan for Sprint 8, because
  it is what a server actually runs.
- **Not required.** The API is an ordinary ASP.NET Core app. Give it a connection string
  and a signing key by any means and it runs — which is exactly what the integration tests
  do.

That last point is worth keeping true. An application that can only run under one
orchestrator has a dependency it does not need.

## Running it

```bash
dotnet run --project src/backend/TeamProjectBoard.AppHost
```

The dashboard URL, with a login token, is printed to the console.

## Related

- [observability.md](observability.md) — what the traces are made of
- [ADR-0010](../adr/0010-aspire-for-local-orchestration.md) — the decision record
