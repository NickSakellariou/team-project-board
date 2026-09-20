# CQRS and Hand-Written Handlers

## What CQRS is (and mostly is not)

**Command Query Responsibility Segregation** in its plainest form: operations that *change*
state and operations that *read* state are separate things, and should be modelled
separately.

That is it. It does not require:

- separate read and write databases,
- event sourcing,
- eventual consistency,
- a message bus,
- a mediator library.

Those all *pair* with CQRS in large systems, which is why the term sounds heavier than it
is. What we use is the plain version: one class per operation, and commands and queries are
different operations.

## Why the split is useful

Commands and queries have genuinely different needs.

A **command** — register a user, move a task — validates input, enforces business rules,
changes state, and usually returns very little. It needs the change tracker, a
transaction, and care about concurrency.

A **query** — get a user, list a board — returns data, changes nothing, and wants to be
fast. It benefits from `AsNoTracking()`, from projecting straight to a DTO rather than
materialising entities, and it can safely read a replica.

Forcing both through one abstraction — the classic `IRepository<T>` with `GetById` and
`Update` — means each gets the other's compromises. Queries drag the change tracker around
for no reason; commands cannot express operations the generic interface never anticipated.

Compare `UserModuleApi.GetUsersAsync`, a query:

```csharp
return await dbContext.Users
    .AsNoTracking()                                    // no change tracking; it is a read
    .Where(user => userIds.Contains(user.Id))
    .Select(user => new UserSummary(...))              // projected in SQL, not in memory
    .ToListAsync(cancellationToken);
```

with `RegisterUserHandler`, a command, which needs tracking, validation, and a rollback
path. Neither would be improved by sharing an abstraction with the other.

## One handler per use case

Every operation gets its own class, its own interface, and its own folder:

```csharp
internal interface IRegisterUserHandler : IHandler
{
    Task<Result<UserResponse>> HandleAsync(RegisterUserRequest request, CancellationToken ct);
}

internal sealed class RegisterUserHandler(
    UserManager<User> userManager,
    ILogger<RegisterUserHandler> logger) : IRegisterUserHandler
```

The alternative is `UserService` with eight methods. Three concrete problems with that:

**Constructor bloat.** `UserService` needs every dependency any of its methods needs.
Registering a user pulls in an email sender the method never touches. `RegisterUserHandler`
declares two dependencies, and they are both used.

**Nobody can tell what is safe to change.** Editing a method on a class eight endpoints
share means checking all eight. A handler has exactly one caller.

**It only grows.** Service classes are where code goes when there is nowhere obvious to put
it. A folder per use case gives every piece of code an obvious home.

The interface is per use case rather than one wide `IHandler<TIn, TOut>` because it keeps
consumers narrow: `RegisterUserEndpoint` depends on registration alone, so a test
substitutes exactly one thing.

## Why no MediatR

MediatR is the usual way to do this in .NET. You send a request object and the library
finds the handler:

```csharp
var result = await mediator.Send(new RegisterUserCommand(email, password));
```

We call the handler directly instead:

```csharp
var result = await handler.HandleAsync(request, cancellationToken);
```

**What MediatR would give us.** Mainly the pipeline: cross-cutting behaviour registered
once and applied to every handler — validation, logging, transactions, caching. That is a
genuine benefit and the honest reason most teams adopt it.

**What it costs.** `mediator.Send(request)` is not navigable. Ctrl-click goes to MediatR's
`Send` method, not to the code that runs. Understanding a request means knowing the
convention rather than following a reference. Stack traces gain several frames of library
internals. And a runtime failure to find a handler replaces a compile error.

**Why we skip it.** With one module and eight slices there is no cross-cutting behaviour to
factor out yet — validation happens in the endpoint, which is explicit and easy to follow.
Paying the indirection cost for a pipeline we are not using is a bad trade. If, by Sprint
5, several modules want the same transaction-and-audit wrapper around every command, that
is the moment to reconsider — either MediatR or a small decorator of our own.

Recorded in [ADR-0003](../adr/0003-cqrs-hand-written-handlers.md).

## How handlers get registered

The one piece of magic we did keep. `RegisterHandlersFromAssemblyContaining` scans the
module's assembly for `IHandler` implementations and registers each against its own narrow
interface:

```csharp
var serviceType = implementationType.GetInterfaces()
    .FirstOrDefault(i => i != typeof(IHandler) && i.IsAssignableTo(typeof(IHandler)));

services.AddScoped(serviceType, implementationType);
```

The gain: adding a slice needs no DI edit, so the whole class of "the endpoint 404s because
someone forgot a line in `Program.cs`" disappears.

The cost, stated plainly: the wiring is no longer greppable. Nothing in the source says
`RegisterUserHandler` is registered. That is why `ConventionTests` exists — it asserts every
handler is named `*Handler`, is `sealed`, is not public, and has a matching endpoint. The
conventions the scan depends on are checked at build time rather than discovered at
runtime.

### Why scoped

One instance per HTTP request. Handlers depend on `DbContext`, which is scoped, and
injecting a scoped service into a singleton is a "captive dependency" — the singleton holds
the first request's `DbContext` forever. See
[dependency-injection.md](dependency-injection.md).

## `IHandler` is empty on purpose

```csharp
public interface IHandler;
```

A marker interface. It declares nothing; its only job is to be findable by the assembly
scan. Analyzers flag empty interfaces (CA1040), and the suppression in `.editorconfig`
explains why this one stays.

## Related

- [anatomy-of-a-slice.md](anatomy-of-a-slice.md) — a handler in its request
- [result-pattern.md](result-pattern.md) — what handlers return
- [dependency-injection.md](dependency-injection.md) — lifetimes and the scan
