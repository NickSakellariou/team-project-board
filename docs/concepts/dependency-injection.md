# Dependency Injection

## What the container does

A class declares what it needs in its constructor:

```csharp
internal sealed class RegisterUserHandler(
    UserManager<User> userManager,
    ILogger<RegisterUserHandler> logger) : IRegisterUserHandler
```

It never constructs those. Something else — the DI container — knows how to build a
`UserManager<User>`, builds one, and passes it in.

That inversion is what makes the class testable: a test passes a substitute instead, and
the class cannot tell the difference. It is also what makes `IAuthenticationService`
useful — the handler names a capability, and which implementation satisfies it is decided
at startup.

## Registration and resolution

**Registration** happens once, at startup, in `Program.cs` and the `Add*Module` methods:

```csharp
services.AddScoped<IAuthenticationService, AuthenticationService>();
```

Read as: "when someone asks for `IAuthenticationService`, give them an
`AuthenticationService`, one per request".

**Resolution** happens when something is needed. The container walks the constructor
parameters, builds each dependency (and *their* dependencies), and hands back the object.
Ask for `RegisterUserHandler` and you get `UserManager`, which pulls in the user store,
which pulls in `UsersDbContext`, which pulls in options and the interceptor — all built in
the right order, none of it written by us.

## The three lifetimes

This is the part that causes real bugs.

| Lifetime | One instance per | Use for |
|---|---|---|
| **Transient** | every request for it | cheap, stateless objects |
| **Scoped** | HTTP request | anything holding per-request state |
| **Singleton** | application | stateless services, expensive-to-build config |

### Why `DbContext` is scoped

A `DbContext` is a unit of work: it accumulates changes and writes them in one transaction
on `SaveChangesAsync`. Its change tracker holds every entity it has loaded.

- **Singleton** would be a catastrophe. Every request would share one change tracker and
  one transaction. Two concurrent requests would see each other's uncommitted entities, and
  one calling `SaveChangesAsync` would commit the other's half-finished work. `DbContext` is
  also not thread-safe, so concurrent use corrupts its internal state.
- **Transient** would break the unit of work. Two components in one request would get two
  contexts and two transactions, so a partial failure could commit half the change.

Scoped is the only correct answer: one per request, so a request is one unit of work.

### Why `AuditableInterceptor` is a singleton

It holds no state. Everything it touches arrives as a method argument:

```csharp
public sealed class AuditableInterceptor : SaveChangesInterceptor
{
    // no fields
    private static void ApplyTimestamps(DbContext context) { ... }
}
```

One instance can serve every request safely. Making it scoped would allocate one per
request for no benefit.

Note the direction: a **singleton interceptor is used by a scoped DbContext**. That is
fine. The reverse is the bug.

### The captive dependency

```csharp
services.AddSingleton<SomeService>();     // built once, lives forever
// SomeService's constructor takes UsersDbContext (scoped)
```

The container builds `SomeService` once, during the first request, and injects *that
request's* `DbContext`. The singleton then holds it forever — after the request ends and
the context is disposed. Every later use fails with `ObjectDisposedException`, or worse,
silently reads stale tracked entities.

It is called a captive dependency because the scoped service is trapped inside the
singleton, unable to be scoped. .NET's container detects the obvious cases at startup when
scope validation is on (it is, in Development), but not all of them.

**Rule of thumb: a service may depend on things with an equal or longer lifetime, never
shorter.**

### Where this bit us in the tests

`UsersApiFactory.ResetDatabaseAsync` cannot inject `UserSeedService` — the factory outlives
any request. It creates a scope instead:

```csharp
using var scope = Services.CreateScope();
var seedService = scope.ServiceProvider.GetRequiredService<UserSeedService>();
```

Same in `Program.cs` for migrations. `CreateScope` is the escape hatch for resolving scoped
services outside a request, and the `using` is what disposes them afterwards.

## `IConfigureOptions<T>`

`AuthorizationConfigureOptions` uses a pattern worth understanding, because it solves a
chicken-and-egg problem.

We want each module to contribute its own authorization policies. But policies are
configured on `AuthorizationOptions` during `AddAuthorization()` — at which point the
container is not yet built, so the `IPolicyFactory` services that know the policies do not
exist.

`IConfigureOptions<T>` is the framework's answer: a registered service that configures a
settings object *later*, when the options are first resolved and the container is
available.

```csharp
services.AddSingleton<IConfigureOptions<AuthorizationOptions>, AuthorizationConfigureOptions>();
```

```csharp
internal sealed class AuthorizationConfigureOptions(
    IEnumerable<IPolicyFactory> policyFactories,   // every module's factory
    ILogger<AuthorizationConfigureOptions> logger)
    : IConfigureOptions<AuthorizationOptions>
{
    public void Configure(AuthorizationOptions options) { ... }
}
```

Injecting `IEnumerable<IPolicyFactory>` gets *all* registered implementations. That is how
the host collects every module's policies without knowing any module exists.

Note the same trick behind `MapApiEndpoints`, which resolves `IEnumerable<IApiEndpoint>`.

## `TryAddEnumerable` and why it is not `Add`

From `MapEndpointExtensions`:

```csharp
services.TryAddEnumerable(serviceDescriptors);
```

Every endpoint registers under the same service type, `IApiEndpoint`. Plain `Add` appends
unconditionally, so if a module's registration ever ran twice, each endpoint would be
registered twice, `MapApiEndpoints` would map each route twice, and ASP.NET Core would
refuse to start with an ambiguous-route error.

`TryAddEnumerable` skips a descriptor whose *implementation type* is already registered —
which is what makes the registration idempotent.

(Note that plain `TryAdd` would be wrong here too: it would register the first endpoint and
skip every other one, because it deduplicates on the service type.)

## Extension methods in `Microsoft.Extensions.DependencyInjection`

Several files carry a namespace that does not match their folder:

```csharp
// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;
```

A deliberate convention: `Program.cs` already has a `using` for that namespace, so
`AddUsersModule` and `AddCoreInfrastructure` appear in IntelliSense with no extra import.
The file still belongs to its own project; only the namespace is borrowed. It is what every
ASP.NET Core library does.

## One registration method per module

```csharp
builder.Services.AddUsersModule(builder.Configuration);
```

The host knows one method name. It does not know `UsersDbContext` exists, or any handler,
or any policy. A module can restructure itself entirely and `Program.cs` does not change —
which is the boundary from [modular-monolith.md](modular-monolith.md), expressed in DI.

## Related

- [cqrs-and-handlers.md](cqrs-and-handlers.md) — how handlers are discovered and registered
- [ef-core-basics.md](ef-core-basics.md) — why the DbContext's lifetime matters
