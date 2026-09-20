# Clean Architecture and Vertical Slices

## The one rule

Clean Architecture is usually drawn as concentric circles. Strip away the diagram and one
rule remains:

> **Source-code dependencies point inward only.** Inner layers know nothing about outer ones.

Everything else follows from that.

In this project the layers are projects, so the compiler enforces it:

```
Modules.Users.Features          ← endpoints, handlers, validators
        │ references
        ▼
Modules.Users.Infrastructure    ← DbContext, EF mappings, JWT service
        │ references
        ▼
Modules.Users.Domain            ← entities, errors, Result<T>
        │ references
        ▼
Modules.Common.Domain           ← references nothing at all
```

`Modules.Common.Domain.csproj` has no `ProjectReference` and no `PackageReference`. That is
not an accident, and it is checked by `ModuleBoundaryTests`.

## Why the direction matters

The domain holds the rules that make this application *this* application: a user has one
system role, a refresh token is single-use, an email registers once. Those rules should
outlive every technical decision around them.

If `User` referenced EF Core, then:

- You could not test a domain rule without a database.
- Swapping Postgres for anything else would mean changing the domain.
- The rules would be tangled with persistence concerns and hard to read as rules.

Keeping the arrow pointing inward means the outer layers are replaceable and the inner
ones are stable. That is the whole return on the extra projects.

### Dependency inversion in practice

The awkward case: a handler needs to issue a JWT, which needs cryptography and
configuration — infrastructure. But Features depends on Infrastructure, so surely that is
fine?

It compiles, but it couples the use case to the mechanism. So the *interface* is declared
in the Domain and the implementation in Infrastructure:

```
Modules.Users.Domain/Authentication/IAuthenticationService.cs   ← the contract
Modules.Users.Infrastructure/Authorization/AuthenticationService.cs   ← the mechanism
```

`LoginUserHandler` depends on the interface. The dependency arrow points inward at
compile time even though the *call* goes outward at run time. That inversion is what makes
`LoginUserHandlerTests` able to run with no cryptography and no configuration.

## Where vertical slices come in

Clean Architecture says how the layers relate. It says nothing about how to organise files
*within* a layer — and the default answer, grouping by technical role, is where layered
codebases go wrong:

```
Features/
  Endpoints/     RegisterUserEndpoint.cs, LoginUserEndpoint.cs, GetUserByIdEndpoint.cs, …
  Handlers/      RegisterUserHandler.cs, LoginUserHandler.cs, GetUserByIdHandler.cs, …
  Validators/    RegisterUserValidator.cs, LoginUserValidator.cs, …
```

Everything about registration is spread across three folders, and each folder mixes a
dozen unrelated features. Vertical slices invert it:

```
Features/Users/
  RegisterUser/    RegisterUser.Endpoint.cs  .Handler.cs  .Validator.cs
  LoginUser/       LoginUser.Endpoint.cs     .Handler.cs  .Validator.cs
  GetUserById/     GetUserById.Endpoint.cs   .Handler.cs
```

Three practical consequences:

- **Working on a feature means opening one folder.** No hunting.
- **Deleting a feature means deleting a folder.** Nothing is left behind — the single most
  reliable sign that the grouping is right.
- **Changing a feature cannot break another.** They share no code.

## The duplication objection

`GetUserById.Handler` and `GetCurrentUser.Handler` are nearly identical. A layered design
would have one `UserService.GetUser(id)` called by both.

We keep both, and this is the central trade of the style, so it deserves a straight answer.

Shared code is coupling. The moment `GetCurrentUser` needs to include something
`GetUserById` must not expose — the user's own email preferences, say — the shared method
grows a parameter, or a flag, or a second overload. Repeat that a few times and you have
the sprawling service class the structure was meant to avoid.

Duplicated code between slices, by contrast, changes independently. That is worth real
repetition.

The line to hold: **duplicate use-case logic freely; do not duplicate domain rules.** If
two slices both need to know that a refresh token is single-use, that rule belongs in the
domain, not copy-pasted into two handlers. The test is whether the two pieces of code
would always have to change together — if yes, it is one rule in the wrong place.

## Where shared things do live

Not everything can be duplicated:

| Shared thing | Where | Why |
|---|---|---|
| Routes | `Users/Shared/Routes/RouteConsts.cs` | So the URL surface is visible in one place and a base-path change is one edit. |
| Response DTOs | `Users/Shared/UserResponse.cs` | Several slices return the same shape to the same client. |
| Errors | `Users.Domain/Errors/UserErrors.cs` | The module's whole failure surface, readable at a glance, with consistent codes. |
| Domain rules | `Users.Domain` | Rules are not use cases. |

The deliberate exception is `RouteConsts`: it is the one file every slice touches, accepted
because inconsistent URLs are worse.

## What each project is for

**`Modules.<X>.Domain`** — entities, value objects, enums, error definitions, and the
interfaces for infrastructure the domain needs. No EF Core, no ASP.NET Core, no HTTP.

**`Modules.<X>.Infrastructure`** — the `DbContext`, EF configurations, migrations, and
implementations of the domain's interfaces. Everything that talks to the outside world.

**`Modules.<X>.Features`** — the slices. Endpoints, handlers, validators, and the module's
DI registration.

**`Modules.<X>.PublicApi`** — contracts other modules may use. References *nothing*, which
is what makes the boundary enforceable. See [modular-monolith.md](modular-monolith.md).

## Is this over-engineered for a side project?

For a CRUD app with one entity, yes, plainly. Four projects to store a user is more
ceremony than the problem needs.

The honest justification here is that this project exists partly to learn the pattern, and
partly because the shape it is heading for — four modules, real-time collaboration,
multi-user concurrency — is where the boundaries start earning their keep. The point at
which you *wish* you had boundaries is well past the point where adding them is cheap.

What matters is that the cost is understood rather than absorbed by reflex. See
[ADR-0002](../adr/0002-clean-architecture-vertical-slices.md).

## Related

- [anatomy-of-a-slice.md](anatomy-of-a-slice.md) — a slice traced end to end
- [modular-monolith.md](modular-monolith.md) — the layer above this one
- [cqrs-and-handlers.md](cqrs-and-handlers.md) — why the handler is its own class
