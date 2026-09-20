# 0010. .NET Aspire for local orchestration

**Status:** Accepted
**Date:** 2026-09-13

## Context

Running the application locally needs Postgres, the API, a connection string joining them,
and a JWT signing key. Adding a step to a README is how that goes stale: nothing checks the
instructions, so they drift until a fresh clone does not work.

Sprint 8 will need a production deployment story regardless.

## Options considered

**Manual setup.** Install Postgres or run a container by hand, create the database, write a
local settings file. Zero tooling, and five documented steps that rot.

**Docker Compose for development.** `docker compose up` starts Postgres and the API. One
command, works everywhere, and it is also the production artifact so there is only one
thing to learn. But the API runs in a container, so debugging means attaching a remote
debugger, and every code change is a rebuild.

**.NET Aspire.** An AppHost project describes the resources in C#. Starts the containers,
injects connection strings, waits for health, and provides a dashboard with logs, traces
and metrics. The API runs on the host, so F5 debugging is normal. Adds a concept and two
projects.

## Decision

Aspire for local development. Docker Compose returns in Sprint 8 for deployment.

Two deliberate differences from the reference template: reference `AddDatabase(...)` rather
than the server resource, so the injected connection string names a database; and configure
OpenTelemetry in `AddCoreInfrastructure` only, not in ServiceDefaults, so instrumentation is
not registered twice.

## Consequences

**Easier**

- One command, and the setup *is* code, so it cannot silently disagree with reality.
- Connection strings are injected as environment variables — never in `appsettings.json`,
  never committed, no ports in source.
- `WaitFor` removes the startup race where the API migrates against a database that is not
  listening yet.
- The dashboard's trace waterfall shows each SQL statement inside each request, which is
  usually where the answer is.
- The signing key has a safe home: the AppHost is never deployed, so a value there cannot
  reach production by accident.

**Harder**

- Another framework to learn, for a benefit that is invisible until the application has
  more than one moving part.
- Two extra projects (AppHost, ServiceDefaults) that ship nothing.
- Aspire is young and its API has moved between versions, so tutorials go out of date.
- Two ways to run things — Aspire locally, Compose in production — which can diverge. The
  mitigation is keeping the API a plain ASP.NET Core app that runs given a connection string
  by any means; the integration tests prove that, since they run it without Aspire.
- Docker is required for local development.

## Related

- [concepts/aspire.md](../concepts/aspire.md)
- [concepts/observability.md](../concepts/observability.md)
