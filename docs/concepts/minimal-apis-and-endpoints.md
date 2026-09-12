# Minimal APIs and Self-Registering Endpoints

## What Minimal APIs are

A way of declaring HTTP endpoints as delegates rather than controller actions:

```csharp
app.MapPost("/api/users/register", HandleAsync);
```

No base class, no attributes, no convention that a class ending in `Controller` is special.
Just a route, a verb and a method.

## Why not controllers

Controllers are not wrong; they fit a different structure. The mismatch here is specific.

**A controller is a class that accumulates endpoints.** `UsersController` would hold
register, login, refresh, me, get-by-id, update, delete, and update-role. That works
against vertical slices, where the point is that everything about one feature lives in one
folder and nothing else shares it.

**Constructor dependencies are shared by every action.** A controller declares what *any*
of its actions need, so registration pulls in dependencies it never touches. Each endpoint
class here declares its own, and the method parameters make it explicit per request.

**Controllers grow.** There is always somewhere to put a new action, so new actions go
there. A folder per use case gives every piece of code an obvious home.

Minimal APIs are also measurably faster — no action-descriptor lookup, no filter pipeline
unless you add one — though at this scale that is a footnote, not a reason.

## The `IApiEndpoint` pattern

The one thing Minimal APIs lack is organisation. Left alone they encourage a `Program.cs`
with fifty `app.MapGet(...)` lines. So each endpoint registers itself:

```csharp
public interface IApiEndpoint
{
    void MapEndpoint(WebApplication app);
}
```

```csharp
public sealed class RegisterUserEndpoint : IApiEndpoint
{
    public void MapEndpoint(WebApplication app)
    {
        app.MapPost(RouteConsts.Register, HandleAsync)
            .AllowAnonymous()
            .WithTags(RouteConsts.Tag)
            .Produces<UserResponse>(StatusCodes.Status201Created);
    }

    private static async Task<IResult> HandleAsync(...) { ... }
}
```

At startup, `RegisterApiEndpointsFromAssemblyContaining` finds every implementation and
`MapApiEndpoints` calls `MapEndpoint` on each. Adding a route means adding a file. Nothing
central is edited, so a merge conflict in `Program.cs` is not a thing that happens.

The trade is the same one the handler scan makes: the wiring is not greppable. `ConventionTests`
compensates by asserting at build time that every `IApiEndpoint` is named `*Endpoint` and
has a matching handler.

## Parameter binding

```csharp
private static async Task<IResult> HandleAsync(
    string userId,                             // from the route template {userId}
    [FromBody] UpdateUserRequest request,      // from the JSON body
    ClaimsPrincipal principal,                 // from HttpContext.User
    IValidator<UpdateUserRequest> validator,   // from DI
    IUpdateUserHandler handler,                // from DI
    CancellationToken cancellationToken)       // from the framework
```

ASP.NET Core works out each source:

- **Route values** — a parameter whose name matches a `{placeholder}` in the template.
- **`[FromBody]`** — deserialized from JSON. At most one per endpoint.
- **Special types** — `HttpContext`, `ClaimsPrincipal`, `CancellationToken` are supplied
  directly.
- **Registered services** — anything else whose type is in the container.

**There is no `[FromServices]`.** Since .NET 7 the framework infers it for any parameter
whose type is a registered service, so writing it is noise. Listed under "patterns we do
not use" in `CLAUDE.md`.

`CancellationToken` is worth taking seriously: it is cancelled when the client disconnects,
so work stops instead of continuing for nobody. Every async call in a request path should
pass it through, which is why `LoginUserHandlerTests` has a test asserting it does.

## Why endpoint methods are `static`

```csharp
private static async Task<IResult> HandleAsync(...)
```

The endpoint class holds no state — everything arrives as a parameter. `static` says so,
and makes it impossible to accidentally add a field that would be shared across requests
by a transient-registered class.

## Route metadata

The fluent calls after `MapPost` attach metadata:

```csharp
.RequireAuthorization(UserPolicyConsts.Read)   // authorization: the policy this needs
.WithTags(RouteConsts.Tag)                     // Swagger: which group to show it under
.WithSummary("Get a user by id.")              // Swagger: the description
.Produces<UserResponse>()                      // Swagger: the success shape
.ProducesProblem(StatusCodes.Status404NotFound)// Swagger: the failure shapes
```

`RequireAuthorization` is the functional one; the rest is documentation. That documentation
earns its place: it is what makes Swagger usable, and what would generate a correct
TypeScript client if the frontend ever generates one from the OpenAPI document.

`.AllowAnonymous()` versus `.RequireAuthorization()` versus `.RequireAuthorization(policy)`
is the whole access-control model, stated one endpoint at a time:

| Declaration | Meaning |
|---|---|
| `.AllowAnonymous()` | anyone — register, login, refresh |
| `.RequireAuthorization()` | any signed-in user — `/me` |
| `.RequireAuthorization(policy)` | a signed-in user holding that claim |

## Routes in one file

Slices are self-contained, which makes it easy to end up with `/api/users/{id}` in one file
and `/api/user/{userId}` in another. `RouteConsts` is the deliberate exception:

```csharp
internal static class RouteConsts
{
    private const string Base = "/api/users";
    internal const string Register = $"{Base}/register";
    internal const string GetById  = $"{Base}/{{userId}}";
}
```

The module's whole URL surface fits on one screen, and changing the base path is one edit.
`{{userId}}` is a doubled brace because the string is interpolated — it produces a literal
`{userId}`.

## The endpoint's job, and what it is not

Every endpoint here does the same four things:

1. Validate the request.
2. Call the handler.
3. Map a failure to a problem response.
4. Map a success to a status code.

No business logic. That belongs in the handler, which knows nothing about HTTP and could
therefore be called from a SignalR hub or a background job unchanged — which matters from
Sprint 5, when the Collaboration module will want exactly that.

## Related

- [anatomy-of-a-slice.md](anatomy-of-a-slice.md) — an endpoint in a full request
- [result-pattern.md](result-pattern.md) — the `ToProblem()` in step 3
- [dependency-injection.md](dependency-injection.md) — how the scan registers them
