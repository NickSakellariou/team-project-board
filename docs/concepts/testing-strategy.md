# Testing Strategy

## The four suites

| Suite | Count | Needs | Speed | Catches |
|---|---|---|---|---|
| `Modules.Common.Tests.Architecture` | 12 | nothing | ~1s | boundary and convention violations |
| `Modules.Common.Tests.Unit` | 14 | nothing | ~150ms | bugs in `Result<T>` |
| `Modules.Users.Tests.Unit` | 18 | nothing | ~600ms | handler and validator logic |
| `Modules.Users.Tests.Integration` | 20 | Docker | ~13s | wiring, auth, SQL, serialization |

Each catches a class of bug the others structurally cannot. That is the reason for four
rather than one.

## Architecture tests

The least familiar and, for a modular monolith, arguably the highest value per line.

```csharp
[Fact]
public void UsersPublicApi_ShouldNotDependOn_TheRestOfItsModule()
{
    var result = Types.InAssembly(ModuleAssemblies.UsersPublicApi)
        .Should()
        .NotHaveDependencyOnAny("Modules.Users.Domain", "Modules.Users.Infrastructure", ...)
        .GetResult();

    Assert.True(result.IsSuccessful, FormatFailure(result));
}
```

NetArchTest reflects over compiled assemblies and asserts on their references.

**Why bother, when project references already enforce most of it?** Because architecture
erodes one reasonable-looking shortcut at a time. Adding a `ProjectReference` takes ten
seconds and always feels justified in the moment. A failing build is the thing that makes
someone stop and ask whether it should be a `PublicApi` contract instead.

They also encode rules the compiler cannot: naming conventions, sealing, visibility.

### They were verified by breaking them

An architecture test that never fails is indistinguishable from one that cannot fail. So
the boundary was deliberately broken — a `PublicApi` type returning a `Domain` entity — and
the suite was checked:

```
Failed  ModuleBoundaryTests.UsersPublicApi_ShouldNotDependOn_TheRestOfItsModule
   Offending types: Modules.Users.PublicApi.BoundaryProbe
```

Then reverted.

### A real limitation, found while doing that

The first attempt at the probe used a constant:

```csharp
public static string RoleName => SystemRoles.Admin;   // const string
```

**The test passed.** C# inlines `const` values at compile time, so the compiled assembly
contains the literal `"Admin"` and no reference to `SystemRoles` at all. There is nothing
in the IL for NetArchTest to see.

Worth knowing, because it defines what these tests can and cannot catch. They see type
references in IL — fields, parameters, return types, locals, base types. They do not see
constants, and they do not see something reached by reflection or by raw SQL naming another
schema's table. Architecture tests are a strong guard, not a complete one.

## Unit tests

Fast, isolated, no I/O. They test logic in one class.

`LoginUserHandlerTests` is a good illustration of why the handler depends on
`IAuthenticationService` rather than doing the work itself:

```csharp
private readonly IAuthenticationService _authenticationService = Substitute.For<IAuthenticationService>();
```

NSubstitute builds a stand-in. The test exercises the use case with no database, no
cryptography and no configuration, in milliseconds. Testability is not the only reason for
that interface, but it is a real one.

Validators are the easiest thing in the codebase to test — pure functions over a request
object — and among the easiest to get subtly wrong, since an inverted rule looks identical
to a correct one at a glance.

### Why NSubstitute and not Moq

Mostly taste; both work. NSubstitute's syntax is lighter (`sub.Method().Returns(x)` rather
than `mock.Setup(m => m.Method()).Returns(x)`) and it matches both reference repos. The
project's `CLAUDE.md` originally said Moq; it now says NSubstitute, because two names for
one job in a convention document is worse than either choice.

## Integration tests

These drive the **real application** over HTTP.

```csharp
public sealed class UsersApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
```

`WebApplicationFactory` boots the actual `Program.cs` — real DI, real middleware pipeline,
real routing — and hands back an `HttpClient` wired straight into it with no network socket
involved.

That is the only way to test:

- **Middleware order.** Swap `UseAuthentication` and `UseAuthorization` and every unit test
  still passes while every protected endpoint returns 401.
- **Authorization policies.** A policy that was never registered, or a claim that never
  reaches the token, looks fine in isolation.
- **Serialization.** Whether the JSON a client receives has the property names it expects.
- **Real SQL.** Whether the LINQ actually translates and the constraints actually hold.

### Testcontainers, not the in-memory provider

EF Core's in-memory provider is tempting and wrong here. It is not a database: no schemas,
no unique constraints, no cascade deletes, no SQL translation. This module depends on all
four. A test against it would pass while production broke.

```csharp
private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
```

Testcontainers starts a real Postgres in Docker and throws it away afterwards. The image
tag is pinned deliberately — `postgres:latest` would mean a suite that starts failing one
morning because the tag moved.

The cost is that Docker becomes a prerequisite, and the suite takes ~13 seconds instead of
milliseconds. Worth it for the only tests that exercise the real thing.

### Respawn between tests

Starting a container takes seconds; deleting rows takes milliseconds. Respawn works out the
foreign-key order and issues the deletes, so each test starts from a known state without
paying for a new container.

```csharp
_respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
{
    SchemasToInclude = ["users"],
    TablesToIgnore = ["role", "role_claim", "migration_history"]
});
```

Roles and their claims survive a reset because they are *configuration*, not test data.
Re-seeding them before every test would be slow, and deleting them would break every
authorization check.

### A bug this actually caused

The first run had 14 passing and 6 failing — every admin test. Each one passed when run
alone.

The cause: Respawn clears the `user` table, which includes the seeded admin. The first test
to run had an admin; every later one did not. The fix is in `ResetDatabaseAsync`, which
re-seeds after the reset.

Worth recording because the symptom is characteristic of shared fixtures: **tests that pass
individually and fail together.** When you see that, suspect shared state before suspecting
the code under test.

## What is not tested, and why

- **`AuthenticationService` has no unit tests.** It is almost entirely calls into Identity
  and the JWT library; a unit test would substitute both and end up asserting that the
  mocks were called. The integration tests cover its real behaviour — rotation, replay
  detection, expiry — through HTTP, which is where it actually matters.
- **No tests for the Common infrastructure DI.** If it were wrong, every integration test
  would fail.
- **No load or performance tests.** Nothing to compare against yet.

## Naming

`[Method]_[Scenario]_[ExpectedResult]`:

```
HandleAsync_WithWrongPassword_ReturnsInvalidCredentials
Refresh_ReusingASpentRefreshToken_IsRejected
```

The failure output should say what broke without opening the file. Analyzer rule CA1707
objects to the underscores and is disabled for test projects.

## Running them

```bash
dotnet test src/backend/TeamProjectBoard.slnx              # everything (Docker needed)
dotnet test src/backend/Common/Modules.Common.Tests.Architecture   # ~1s, no Docker
```

## Related

- [modular-monolith.md](modular-monolith.md) — the boundaries being asserted
- [ef-core-basics.md](ef-core-basics.md) — why a real database matters
