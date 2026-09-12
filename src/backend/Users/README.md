# Users Module

Owns accounts, system roles and authentication. The only module that depends on nothing.

## What it owns

- User accounts — creation, profile, deletion
- Password storage and verification (via ASP.NET Core Identity)
- System roles: `Admin` and `User`
- The permissions those roles carry (`users:read`, `users:create`, `users:update`, `users:delete`)
- JWT access tokens and rotating refresh tokens

## What it deliberately does not own

- **Project membership and project roles** (Owner / Member) — those belong to the Projects
  module. A user's role differs per project, so it cannot live on the user.
- **Anything about boards or tasks.** There is no navigation property from `User` to
  anything outside this module; a query here can never reach into another module's tables.

## Projects

| Project | Contains |
|---|---|
| `Modules.Users.Domain` | `User`, `Role`, `RefreshToken`, `UserErrors`, `UserPolicyConsts`, `IAuthenticationService` |
| `Modules.Users.Infrastructure` | `UsersDbContext`, EF mappings, migrations, `AuthenticationService`, `UsersPolicyFactory` |
| `Modules.Users.Features` | The vertical slices, `UserModuleApi`, `AddUsersModule` |
| `Modules.Users.PublicApi` | `IUserModuleApi` and `UserSummary` — the only things other modules may use |
| `Modules.Users.Tests.Unit` | Handler and validator tests |
| `Modules.Users.Tests.Integration` | Full HTTP tests against a real Postgres |

## Endpoints

| Method | Route | Access |
|---|---|---|
| POST | `/api/users/register` | anonymous |
| POST | `/api/users/login` | anonymous |
| POST | `/api/users/refresh` | anonymous |
| GET | `/api/users/me` | any signed-in user |
| GET | `/api/users/{userId}` | `users:read` |
| PUT | `/api/users/{userId}` | `users:update` |
| DELETE | `/api/users/{userId}` | `users:delete` |
| PUT | `/api/users/{userId}/role` | `users:update` |

The three anonymous endpoints are the authentication flow — you cannot hold a token before
you have one. `/me` needs only a valid token; everything else is administrative and needs a
claim.

## What other modules may use

```csharp
public interface IUserModuleApi
{
    Task<bool> UserExistsAsync(string userId, CancellationToken ct);
    Task<IReadOnlyList<UserSummary>> GetUsersAsync(IReadOnlyCollection<string> userIds, CancellationToken ct);
}
```

That is the whole surface. `UserSummary` is `(Id, Email, DisplayName)` — no password hash,
no lockout state, no audit fields.

`GetUsersAsync` takes a collection rather than a single id because the real question is
"who are the twelve members of this board?", and a single-id method would be called in a
loop.

## Database

Schema `users`, nine tables, all snake_case. Seven come from Identity (renamed from their
`AspNet*` defaults), plus `refresh_token` and this module's own `migration_history`.

```
users.user            users.role          users.user_role
users.user_claim      users.role_claim    users.user_login
users.user_token      users.refresh_token users.migration_history
```

`user_claim`, `user_login` and `user_token` are unused. They cost nothing empty and would
be a migration to add later.

## Seeded data

`UserSeedService` runs at startup in Development. It creates the two roles, attaches the
four `users:*` claims to `Admin`, and creates a development admin from `Seed:AdminEmail`
and `Seed:AdminPassword`.

This is not optional: registration assigns the `User` role, so without seeding the first
registration fails, and the policies would reject everyone because no role carries their
claims.

## Gotchas

- **Demoting a user is not immediate.** Permissions live in their access token, which stays
  valid for up to `AccessTokenMinutes` (15). Inherent to stateless tokens — see
  [ADR-0008](../../../docs/adr/0008-identity-and-jwt.md).
- **A replayed refresh token logs out the real user too.** Deliberate: at that point the
  legitimate holder and the attacker are indistinguishable.
- **Login returns the same error for an unknown email and a wrong password.** Otherwise the
  endpoint becomes a way to discover which addresses have accounts.
- **A user cannot delete their own account or change their own role.** Cheapest guard
  against an organisation removing its last admin.

## Further reading

- [Anatomy of a slice](../../../docs/concepts/anatomy-of-a-slice.md) — `POST /register`, traced end to end
- [ASP.NET Core Identity](../../../docs/concepts/aspnet-core-identity.md)
- [Authentication and JWTs](../../../docs/concepts/authentication-and-jwt.md)
