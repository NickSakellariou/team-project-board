# Log event ids

Every log event in this solution is declared in a **catalogue** class and carries a **stable
numeric event id**. This note records which ranges exist, who owns them, and what each
allocated id means.

If you are adding an event rather than reading about one, use the `adding-a-log` skill —
it is the checklist version of this page.

## Why ids at all

A log statement written inline is anonymous. The only handle on it is its text, so the day
someone rewords `"User {UserId} signed in"` to `"Sign-in succeeded for {UserId}"`, every
saved query, dashboard panel and alert built on the old wording stops matching — and
nothing fails, anywhere, until somebody notices the graph has been flat for a month.

An event id is the handle that survives rewording, renaming and moving the code. It also
gives the event a name in prose: "10005" is a thing you can put in a runbook, an alert
description or a postmortem.

Three things follow, and they are the rules:

- **An id is permanent.** Once allocated, it is never reused for a different event and never
  renumbered, even if the event is deleted. A retired id stays retired.
- **An id belongs to exactly one event.** Two events sharing an id make every count drawn
  from it wrong, in a way no test in the emitting code could ever see.
- **The level is part of the contract.** An event declares its own level; it never takes one
  from the call site. An alert on "Warning or above for 10005" is only meaningful if 10005
  is *always* a Warning.

## Where events are declared

One catalogue per project that logs, named `<Area>Logs`, holding nothing but
`[LoggerMessage]` declarations. It is the same shape as the error catalogues
(`UserErrors`, `RequestErrors`) and exists for the same reason: the complete surface is
readable in one file, and wording is changed in one place.

| Catalogue | File | Range |
|---|---|---|
| `CommonApiLogs` | `Common/Modules.Common.API/Logging/` | 1000–1999 |
| `CommonApplicationLogs` | `Common/Modules.Common.Application/Logging/` | 2000–2999 |
| `CommonInfrastructureLogs` | `Common/Modules.Common.Infrastructure/Logging/` | 3000–3999 |
| `HostLogs` | `TeamProjectBoard.Host/Logging/` | 4000–4999 |
| `UserLogs` | `Users/Modules.Users.Domain/Logging/` | 10000–10999 |

Ranges 5000–9999 are left free for further shared layers. Each module then gets a
thousand, starting at 10000:

| Range | Module | Status |
|---|---|---|
| 10000–10999 | Users | in use |
| 11000–11999 | Projects | reserved |
| 12000–12999 | Boards | reserved |
| 13000–13999 | Collaboration | reserved |

A module's catalogue lives in its **Domain** project, because both Features and
Infrastructure emit events and Domain is the only layer both can see — the same reasoning
that puts `UserErrors` there.

## The allocation

### 1000–1999 — `CommonApiLogs`

| Id | Name | Level | Message | Emitted from |
|---|---|---|---|---|
| 1000 | `UnhandledException` | Error | `Unhandled exception while processing {Method} {Path}` | `GlobalExceptionHandler` |

Worth alerting on. Expected failures come back as a failed `Result<T>` and never reach this
handler, so anything carrying 1000 is a bug or infrastructure being down.

### 2000–2999 — `CommonApplicationLogs`

| Id | Name | Level | Message | Emitted from |
|---|---|---|---|---|
| 2000 | `ValidationFailed` | Warning | `{ContextMessage}: {ValidationErrors}` | `ValidationExtensions.LogValidationErrors` |

One event for every use case, not one per validator. The use case is a structured field, so
"show me every failed registration" stays a query and no id has to be allocated per slice.

### 3000–3999 — `CommonInfrastructureLogs`

| Id | Name | Level | Message | Emitted from |
|---|---|---|---|---|
| 3000 | `AuthorizationPoliciesRegistered` | Information | `Registered {PolicyCount} authorization policies for module {ModuleName}` | `AuthorizationConfigureOptions` |

### 4000–4999 — `HostLogs`

Startup and seeding. All emitted from `TeamProjectBoard.Host/Seeding/UserSeedService.cs`.

| Id | Name | Level | Message |
|---|---|---|---|
| 4000 | `RoleCreated` | Information | `Created role {RoleName}` |
| 4001 | `PermissionGranted` | Information | `Granted {Permission} to the {RoleName} role` |
| 4002 | `SeedAdminNotConfigured` | Information | `No seed admin configured; skipping.` |
| 4003 | `SeedAdminCreationFailed` | Error | `Could not seed the admin account: {IdentityErrors}` |
| 4004 | `SeededDevelopmentAdmin` | **Warning** | `Seeded development admin {Email}. This account exists only in Development.` |

4004 is a Warning on purpose, and it is the one line on this page worth memorising: an
account with a known password now exists. Outside Development, 4004 is an incident.

### 10000–10999 — `UserLogs`

Sub-blocks keep related events together, so a new authentication event does not land
between two registration ones.

**10000–10099 — authentication.** All emitted from
`Users/Modules.Users.Infrastructure/Authorization/AuthenticationService.cs`.

| Id | Name | Level | Message |
|---|---|---|---|
| 10000 | `LoginAttemptedForUnknownEmail` | Information | `Login attempted for unknown email {Email}` |
| 10001 | `LoginAttemptedForLockedOutUser` | Warning | `Login attempted for locked-out user {UserId}` |
| 10002 | `LoginFailed` | Information | `Failed login for user {UserId}` |
| 10003 | `UserSignedIn` | Information | `User {UserId} signed in` |
| 10004 | `RefreshTokenNotFound` | Warning | `Refresh attempted with an unknown token for user {UserId}` |
| 10005 | `RefreshTokenReplayDetected` | **Warning** | `Refresh token replay detected for user {UserId}. Invalidating all of their tokens.` |
| 10006 | `RefreshTokenAccessTokenMismatch` | Warning | `Refresh token does not match the supplied access token for user {UserId}` |
| 10007 | `AccessTokenRejectedDuringRefresh` | Information | `Rejected an access token during refresh` |

10005 is the event to alert on. A used refresh token coming back is either a buggy client or
a stolen token being replayed, and nothing in the application can tell which — so it assumes
the worst and cuts off the whole chain. A rise in 10005 is a security signal.

Note the asymmetry between 10000 and 10003: the failure path records the email because it is
the only identifier there is, and the success path records only the id. See
[concepts/observability.md](concepts/observability.md#what-must-never-be-logged).

**10100–10199 — account lifecycle.**

| Id | Name | Level | Message | Emitted from |
|---|---|---|---|---|
| 10100 | `RegistrationRejected` | Information | `Registration rejected for {Email}: {IdentityErrors}` | `RegisterUser.Handler` |
| 10101 | `DefaultRoleAssignmentFailed` | Error | `Created user {UserId} but could not assign the default role. The account was rolled back.` | `RegisterUser.Handler` |
| 10102 | `UserRegistered` | Information | `Registered user {UserId}` | `RegisterUser.Handler` |
| 10103 | `UserProfileUpdated` | Information | `Updated profile for user {UserId}` | `UpdateUser.Handler` |
| 10104 | `UserRoleChanged` | Information | `User {UserId} role changed to {Role} by {CallerId}` | `UpdateUserRole.Handler` |
| 10105 | `UserDeleted` | Information | `User {UserId} was deleted by {CallerId}` | `DeleteUser.Handler` |

10101 is an Error rather than a Warning because it only fires when role seeding never ran —
the application is misconfigured and no registration can succeed.

### Retired ids

None yet. When an event is deleted, move its row here with a note rather than removing it,
so the id is visibly spent.

## What enforces all this

Four things, in order of how early they catch a mistake:

1. **`CA1848` is an error** (see `.editorconfig`). A bare `logger.LogInformation(...)` does
   not compile. This is what keeps the catalogues complete — an event cannot exist outside
   one.
2. **Per-catalogue tests** — one test file per catalogue, pinning the id, name, level and
   rendered message of every event. Each file also asserts that no event in its catalogue
   lacks a test, so adding an event and forgetting its test fails the suite.
   - `Common/Modules.Common.Tests.Unit/Logging/`
   - `Users/Modules.Users.Tests.Unit/Logging/UserLogsTests.cs`
   - `TeamProjectBoard.Host.Tests.Unit/Logging/HostLogsTests.cs`
3. **`Modules.Common.Tests.Architecture/LogCatalogueTests.cs`** — the rules no single
   catalogue can check: ids unique across the whole solution, every id inside its own
   catalogue's range, no two ranges overlapping, every event declaring a level, every
   template using PascalCase placeholders.
4. **This page**, which is the only part a test cannot check. Keeping it accurate is a step
   in the `adding-a-log` skill.

## Related

- [concepts/observability.md](concepts/observability.md) — logs vs traces vs metrics, and
  what must never be logged
- [adr/0004-result-pattern.md](adr/0004-result-pattern.md) — the error catalogues these are
  modelled on
