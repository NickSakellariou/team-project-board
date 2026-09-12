# 0009. Claim-based policies contributed per module

**Status:** Accepted
**Date:** 2026-09-13

## Context

Endpoints need access control. There are two levels of role in this application: system
roles (Admin / User), and later project roles (Owner / Member), which vary per project and
so cannot live on the user.

Each module will define its own permissions. The host should not have to know what they are.

## Options considered

**`RequireRole("Admin")` on each endpoint.** Simplest, and it bakes today's org chart into
the code. Introduce a Support role that may read users but not delete them, and every
endpoint needs editing. The endpoint also states *who* rather than *what*, which is the
wrong thing for it to know.

**Claim-based policies, declared centrally.** Endpoints require a permission
(`users:delete`); `Program.cs` declares every policy in one `AddAuthorization` call. The
indirection is right, but a module's permission names end up in a file the module does not
own — and the host grows a line for every permission anyone adds.

**Claim-based policies, contributed per module.** Each module implements `IPolicyFactory`;
the host collects them all at startup without knowing what they are.

## Decision

Claim-based policies via `IPolicyFactory`. Permissions are named `<module>:<action>`
(`users:read`) so modules cannot collide. `UserSeedService` attaches them to roles as
`role_claim` rows, and `AuthenticationService` copies a user's role claims into their JWT
at login.

## Consequences

**Easier**

- An endpoint states the permission it needs, not who has it. Changing what an Admin may do
  is a change to the seed data, with no endpoint touched.
- Modules define their own permissions. Boards will add `boards:*` without editing
  `Program.cs`.
- Authorization is a claim lookup on an already-parsed token — no database read per request.
- Room for per-user permissions later: `user_claim` already exists, and a claim on the user
  is checked by the same policy.

**Harder**

- **Permissions are stale for up to the access-token lifetime.** They are flattened into the
  token at login, so revoking one does not take effect until the next token. That staleness
  is the price of not reading the database on every request, and it is bounded by
  `AccessTokenMinutes`. See [ADR-0008](0008-identity-and-jwt.md).
- More indirection than `RequireRole`. Answering "who can delete a user?" means reading the
  seed service, not the endpoint.
- Roles and permissions must be seeded, or every policy denies everyone. The very first
  registration also fails, because it assigns a role that does not exist. This is why
  seeding runs at startup, and why the integration tests re-seed after every reset.
- `IPolicyFactory` needs the `IConfigureOptions<AuthorizationOptions>` pattern to run after
  the container is built, which is a piece of framework knowledge the code now depends on.

## Related

- [concepts/authentication-and-jwt.md](../concepts/authentication-and-jwt.md)
- [concepts/dependency-injection.md](../concepts/dependency-injection.md)
