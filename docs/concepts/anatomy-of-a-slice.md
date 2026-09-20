# Anatomy of a Slice

**Start here.** This note follows one HTTP request — `POST /api/users/register` — from the
socket to the database row and back. Every layer of the architecture appears exactly once,
in the order the request meets it. Once this is clear, every other feature in the codebase
is the same shape with different nouns.

Open these three files alongside it:

- `src/backend/Users/Modules.Users.Features/Users/RegisterUser/RegisterUser.Endpoint.cs`
- `src/backend/Users/Modules.Users.Features/Users/RegisterUser/RegisterUser.Handler.cs`
- `src/backend/Users/Modules.Users.Features/Users/RegisterUser/RegisterUser.Validator.cs`

Three files, one folder. That folder is the slice.

---

## What "vertical slice" means

The traditional way to organise a web API is by technical role:

```
Controllers/    UserController.cs, ProjectController.cs, BoardController.cs
Services/       UserService.cs, ProjectService.cs, BoardService.cs
Repositories/   UserRepository.cs, ProjectRepository.cs, BoardRepository.cs
Models/         User.cs, Project.cs, Board.cs
```

Adding "register a user" then means editing four files in four folders. Every one of them
also contains code for a dozen unrelated features, so each edit risks something you were
not thinking about. `UserService` grows until it is the only place anybody looks and
nobody understands.

A vertical slice groups by *feature* instead:

```
Users/RegisterUser/      RegisterUser.Endpoint.cs, .Handler.cs, .Validator.cs
Users/LoginUser/         LoginUser.Endpoint.cs, .Handler.cs, .Validator.cs
Users/GetUserById/       GetUserById.Endpoint.cs, .Handler.cs
```

Everything registration does is in one folder. Delete the folder and registration is gone,
with nothing left behind. Change registration and nothing else can break, because nothing
else shares the code.

The trade is real and worth naming: **slices duplicate.** `GetUserById.Handler` and
`GetCurrentUser.Handler` are nearly identical. In a layered design they would share one
method. Here they do not, and that is deliberate — the day one of them needs to change,
it changes alone. Shared code is coupling with a friendly face.

---

## The journey of one request

### 1. The request arrives

```http
POST /api/users/register
Content-Type: application/json

{ "email": "nick@example.com", "password": "Password1", "displayName": "Nick" }
```

Kestrel accepts it and hands it to the middleware pipeline built at the bottom of
`TeamProjectBoard.Host/Program.cs`. Order matters, and each piece runs in the order it is
written:

```
UseExceptionHandler   → catches anything the rest throws
UseSwagger / UseCors  → development only
UseSerilogRequestLogging
UseAuthentication     → reads the JWT, populates HttpContext.User
UseAuthorization      → decides whether that user may proceed
MapApiEndpoints       → routes to the endpoint
```

Authentication finds no token here, and that is fine: the endpoint declared
`.AllowAnonymous()`, so authorization lets it through. You cannot be signed in before you
have an account.

### 2. Routing finds the endpoint

There is no controller. During startup, `RegisterApiEndpointsFromAssemblyContaining`
scanned the Features assembly for classes implementing `IApiEndpoint` and registered each
one; `MapApiEndpoints` then called `MapEndpoint` on all of them. `RegisterUserEndpoint`
used its turn to say:

```csharp
app.MapPost(RouteConsts.Register, HandleAsync).AllowAnonymous()
```

So the route table already contains this path. Note what did *not* happen: nobody added a
line to `Program.cs`. The slice registered itself by existing.

### 3. Parameter binding fills in the arguments

```csharp
private static async Task<IResult> HandleAsync(
    [FromBody] RegisterUserRequest request,
    IValidator<RegisterUserRequest> validator,
    IRegisterUserHandler handler,
    CancellationToken cancellationToken)
```

Minimal APIs work out where each argument comes from:

- `[FromBody]` — deserialize the JSON into the record.
- `IValidator<>` and `IRegisterUserHandler` — resolved from the DI container, because those
  types are registered services. **There is no `[FromServices]` attribute:** since .NET 7
  the framework infers it for any parameter whose type is registered, and writing it adds
  noise.
- `CancellationToken` — supplied by the framework. It is cancelled if the client hangs up,
  so work stops instead of continuing for nobody.

### 4. Validation

```csharp
var validationResult = await validator.ValidateAsync(request, cancellationToken);
if (!validationResult.IsValid)
{
    return Results.ValidationProblem(validationResult.ToDictionary());
}
```

`RegisterUserRequestValidator` checks the request's *shape*: email is present and looks
like an email, password is 8–128 characters, display name is not blank. If any rule fails,
the request stops here with a 400 and never reaches the handler.

The line this draws matters. Validation answers questions that need nothing but the
request object. "Is this email already taken?" is not one of those — it needs the database
and its answer changes over time. That is a business rule, and it belongs in the handler.

### 5. The handler runs the use case

```csharp
var result = await handler.HandleAsync(request, cancellationToken);
```

`IRegisterUserHandler` is an interface declared in the same file as its implementation, and
it has exactly one method. The endpoint depends on that one use case and nothing wider — a
test can substitute registration alone.

Nothing registered this handler by hand either. At startup
`RegisterHandlersFromAssemblyContaining` found every `IHandler` implementation and
registered each against its own narrow interface, as **scoped** — one instance per request,
matching the lifetime of the `DbContext` it depends on.

Inside `RegisterUserHandler`:

```csharp
var createResult = await userManager.CreateAsync(user, request.Password);
```

`UserManager<User>` comes from ASP.NET Core Identity, and that single call normalizes the
email, hashes the password with PBKDF2, generates the security and concurrency stamps,
enforces the password policy and the unique-email rule, and saves. See
[aspnet-core-identity.md](aspnet-core-identity.md) for what each of those means.

### 6. EF Core turns objects into SQL

`UserManager` writes through `UsersDbContext`, which is where the module's persistence
decisions live:

- **Change tracking** — the context watched `user` from the moment it was added and knows
  it needs an INSERT.
- **`AuditableInterceptor`** — hooks `SaveChanges` and stamps `CreatedAtUtc`, so no handler
  ever writes that line.
- **Snake-case naming** — `UseSnakeCaseNamingConvention()` turns `NormalizedEmail` into
  `normalized_email`, so the database reads as ordinary SQL.
- **Schema** — `HasDefaultSchema("users")` puts every table in this module's own schema.
  Nothing lands in `public`.

The SQL is roughly:

```sql
INSERT INTO users."user" (id, email, normalized_email, user_name, normalized_user_name,
                          display_name, password_hash, security_stamp, concurrency_stamp,
                          created_at_utc, ...)
VALUES (@p0, @p1, ...);
```

### 7. The result comes back as a value, not an exception

The handler returns `Result<UserResponse>` — either the created user or one or more
`Error`s. Failure is part of the method's signature rather than something that happens to
the call stack.

```csharp
if (!createResult.Succeeded)
{
    return UserErrors.RegistrationFailed(createResult.Errors);  // implicitly a failed Result
}

return new UserResponse(user.Id, user.Email!, user.DisplayName, [SystemRoles.User]);
```

Both lines look like ordinary returns because of the implicit conversions on `Result<T>`.
See [result-pattern.md](result-pattern.md).

Note that `UserResponse` is a DTO, not the `User` entity. Returning the entity would
serialize `PasswordHash` and `SecurityStamp` straight to the client.

### 8. The endpoint turns the result into HTTP

```csharp
return result.IsError
    ? result.Errors.ToProblem()
    : Results.Created($"/api/users/{result.Value!.Id}", result.Value);
```

`ToProblem()` is the only place in the solution that maps domain failures to status codes:
`Conflict → 409`, `NotFound → 404`, `Validation → 400`. The domain never mentions HTTP, so
the same handler could sit behind a SignalR hub or a background job unchanged.

A duplicate email produces:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "One or more validation errors occurred.",
  "status": 409,
  "errors": { "Users.RegistrationFailed": ["Email 'nick@example.com' is already taken."] }
}
```

The body is keyed by error *code*, so a client branches on `Users.RegistrationFailed`
rather than string-matching prose that might be translated tomorrow.

---

## The shape, in one diagram

```
HTTP request
    │
    ▼
Middleware pipeline ......... Program.cs
    │
    ▼
Endpoint .................... RegisterUser.Endpoint.cs      ─┐
    │  binds, validates, maps the result to a status code    │
    ▼                                                        │  Features
Validator ................... RegisterUser.Validator.cs      │  (the slice)
    │  structural rules only                                 │
    ▼                                                        │
Handler ..................... RegisterUser.Handler.cs       ─┘
    │  the use case; returns Result<T>
    ▼
UserManager / DbContext ..... Modules.Users.Infrastructure
    │
    ▼
User, Result<T>, UserErrors . Modules.Users.Domain
    │
    ▼
PostgreSQL (users schema)
```

Dependencies only ever point **downward**. The domain at the bottom knows nothing about
anything above it, which is why it can be tested with no database and no web server.

---

## Adding your own slice

Copy the pattern:

1. Create `Users/<UseCase>/`.
2. `<UseCase>.Endpoint.cs` — the request record and an `IApiEndpoint`.
3. `<UseCase>.Handler.cs` — an internal `I<UseCase>Handler : IHandler` and a
   `sealed` implementation returning `Result<T>`.
4. `<UseCase>.Validator.cs` — if there is anything structural to check.
5. Add the route to `Users/Shared/Routes/RouteConsts.cs`.

Register nothing. The startup scans find all three by convention, and the architecture
tests in `Modules.Common.Tests.Architecture` fail the build if a name does not match — so
a slice that would have silently failed to register instead fails to compile.

---

## Related

- [clean-architecture-and-slices.md](clean-architecture-and-slices.md) — why the layers are ordered this way
- [result-pattern.md](result-pattern.md) — the `Result<T>` used in step 7
- [aspnet-core-identity.md](aspnet-core-identity.md) — what `UserManager` did in step 5
- [cqrs-and-handlers.md](cqrs-and-handlers.md) — why the handler exists at all
