---
name: adding-a-log
description: Add, change, move or delete a log event in this solution. Use whenever something should be written to the log — a new logger call, a reworded message, a changed level, a log statement moved between classes, or a bare logger.LogInformation/LogWarning/LogError that fails the build with CA1848. Also use when picking an event id or updating docs/log-event-ids.md.
---

# Adding a log event

Every log event in this solution is declared in a **catalogue** class and carries a
permanent numeric **event id**. A bare `logger.LogInformation(...)` does not compile:
`CA1848` is an error in `src/backend/.editorconfig`, on purpose.

The reference is `docs/log-event-ids.md` — the ranges, the full allocation, and the
reasoning. This skill is the procedure.

## Before anything else: does it need a new event?

Do not add one if:

- **An existing event already says it.** Check the relevant catalogue first. A reworded
  duplicate is worse than no event, because now two ids mean one thing.
- **It varies only by subject.** One event with a structured field beats one event per
  case. `ValidationFailed` (2000) covers every use case in the solution because the use
  case is a `{ContextMessage}` field, not part of the template.
- **A trace would answer it better.** "How long did this take" and "what did this request
  touch" are trace questions. See `docs/concepts/observability.md`.

## The six steps

### 1. Pick the catalogue

One catalogue per project that logs, named `<Area>Logs`:

| Where the code lives | Catalogue |
|---|---|
| `Modules.Common.API` | `CommonApiLogs` |
| `Modules.Common.Application` | `CommonApplicationLogs` |
| `Modules.Common.Infrastructure` | `CommonInfrastructureLogs` |
| `TeamProjectBoard.Host` | `HostLogs` |
| Anywhere in the Users module | `UserLogs`, in `Modules.Users.Domain/Logging/` |

A module's catalogue lives in its **Domain** project, so Features and Infrastructure can
both reach it — the same reasoning that puts `UserErrors` there.

**A new module needs a new catalogue.** Create
`Modules.<Module>.Domain/Logging/<Module>Logs.cs`, give the Domain project a
`PackageReference` to `Microsoft.Extensions.Logging.Abstractions`, claim the module's
thousand from the reserved table in `docs/log-event-ids.md`, and add the module's
assemblies to `CatalogueAssemblies` in
`Common/Modules.Common.Tests.Architecture/LogCatalogueTests.cs` — otherwise the
cross-catalogue rules silently do not see it.

### 2. Pick the id

- Take the **next free number in the catalogue's sub-block**, never the next number
  overall. `UserLogs` splits its thousand into authentication (10000–10099) and account
  lifecycle (10100–10199); a new authentication event goes at 10008, not 10106.
- **Never reuse a retired id**, and never renumber an existing one. If an event is deleted,
  move its row to the "Retired ids" section of `docs/log-event-ids.md` rather than deleting
  it, so the number is visibly spent.
- If a sub-block is full or the event fits none of them, open a new sub-block inside the
  catalogue's range and give it a comment banner like the existing ones.

### 3. Declare it

```csharp
/// <summary>One sentence: what happened, in the past tense.</summary>
/// <param name="logger">The logger to write to.</param>
/// <param name="userId">The user who signed in.</param>
[LoggerMessage(
    EventId = 10_003,
    Level = LogLevel.Information,
    Message = "User {UserId} signed in")]
public static partial void UserSignedIn(this ILogger logger, string userId);
```

Rules the tests enforce:

- **Placeholders are PascalCase.** They are field names in the log store, not C#
  parameters. `{UserId}`, never `{userId}`.
- **The template is never interpolated.** `$"...{userId}"` compiles and silently destroys
  the structure.
- **`Level` is always set on the attribute**, never taken as a method parameter. An alert on
  a level is only meaningful if the event always arrives at that level.
- An exception goes in an `Exception` parameter, not in the template.

Choosing the level:

| Level | For |
|---|---|
| Information | Something normal happened that someone may later need to account for |
| Warning | Something suspicious or degraded, but the request was handled |
| Error | A bug or a misconfiguration — someone has to do something |

If the event is one you would want to alert on, say so in a `<remarks>` on the declaration
and in `docs/log-event-ids.md`. `RefreshTokenReplayDetected` (10005) is the worked example.

**Never log** a password, a token, a password hash or a security stamp. Note the deliberate
asymmetry in `UserLogs`: a failed login records the email, because it is the only identifier
there is; a successful one records only the id.

### 4. Call it

```csharp
logger.UserSignedIn(user.Id);
```

The class keeps its `ILogger<T>` constructor parameter exactly as before — the extension
method is on `ILogger`, which `ILogger<T>` is.

### 5. Test it

Every catalogue has one test file, and it must gain a test named `<EventName>_...`:

| Catalogue | Test file |
|---|---|
| `CommonApiLogs`, `CommonApplicationLogs`, `CommonInfrastructureLogs` | `Common/Modules.Common.Tests.Unit/Logging/` |
| `HostLogs` | `TeamProjectBoard.Host.Tests.Unit/Logging/HostLogsTests.cs` |
| `UserLogs` | `Users/Modules.Users.Tests.Unit/Logging/UserLogsTests.cs` |

```csharp
[Fact]
public void UserSignedIn_IsEvent10003()
{
    _logger.UserSignedIn(UserId);

    var record = Assert.Single(_logger.Collector.GetSnapshot());
    Assert.Equal(10_003, record.Id.Id);
    Assert.Equal(nameof(UserLogs.UserSignedIn), record.Id.Name);
    Assert.Equal(LogLevel.Information, record.Level);
    Assert.Equal($"User {UserId} signed in", record.Message);
}
```

`_logger` is a `FakeLogger` from `Microsoft.Extensions.Diagnostics.Testing`. The naming
matters: each test file has an `EveryEvent_HasATest` test that looks for a test method
starting `<EventName>_`, so an event with no test fails the suite.

Add a second test where the event has a property worth stating outright — a level that is
deliberately higher than it looks (`SeededDevelopmentAdmin_IsEvent4004AtWarning`), or a
field that must stay out of the message (`UserSignedIn_DoesNotRecordTheEmail`).

### 6. Document it

Add the row to the right table in `docs/log-event-ids.md`: id, name, level, message, and
where it is emitted from. This is the one part no test can check.

## Then

```bash
dotnet build src/backend/TeamProjectBoard.slnx        # CA1848 and the analyzers
dotnet test  src/backend/Common/Modules.Common.Tests.Architecture
dotnet test  src/backend/<the test project for your catalogue>
```

`Modules.Common.Tests.Architecture/LogCatalogueTests.cs` is the one that catches a duplicate
id, an id outside its range, a missing level or a lowercase placeholder. It is the only
place that sees every catalogue at once, so run it whatever you touched.

## Changing or deleting an existing event

- **Rewording the message** is allowed and is the reason ids exist. Update the declaration,
  the test's expected string, and the row in `docs/log-event-ids.md`.
- **Changing the level** breaks any alert built on it. Do it deliberately, say why in a
  `<remarks>`, and mention it in the PR description.
- **Changing the id** is not a rewording, it is deleting one event and adding another.
  Retire the old id.
- **Moving the code that emits it** changes nothing. The declaration and the id stay put;
  that is the whole point.
- **Deleting an event**: remove the declaration, its test and its call site, then move its
  row to "Retired ids" in `docs/log-event-ids.md`. The number is never handed out again.
