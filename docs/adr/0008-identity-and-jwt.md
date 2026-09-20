# 0008. ASP.NET Core Identity with JWT and rotating refresh tokens

**Status:** Accepted
**Date:** 2026-09-13

## Context

Users need accounts and a way to prove who they are on each request. The frontend is a
React SPA, so the mechanism must work without cookies-and-redirects. Sprint 5 adds SignalR,
which will need the same identity.

Two separable decisions: how accounts and passwords are stored, and how a client proves
identity per request.

## Options considered — account storage

**Hand-rolled users table.** Full control, no framework types in the domain. Requires
getting password hashing right (a deliberately slow KDF, per-user salt, a tuned iteration
count, and a versioned format so the count can be raised later), fixed-time comparison,
normalization for case-insensitive lookup, lockout, and enumeration resistance. Every one
is a security decision where being nearly right is being wrong.

**ASP.NET Core Identity.** All of the above, reviewed and patched. Costs a framework
dependency in the domain and seven tables, four of which we do not use.

**An external identity provider** (Auth0, Entra ID, Keycloak). Removes the problem
entirely. Adds an external dependency, a cost, and — for a project whose stated purpose is
learning how this works — removes the thing being learned.

## Options considered — per-request identity

**Cookies.** The default with `AddIdentity`. Revocable and simple, and it makes an
unauthenticated API call return a 302 to a login page rather than a 401. Wrong shape for an
SPA.

**JWT only.** Stateless, fast, and *unrevocable*. A stolen or stale token works until it
expires.

**JWT plus a database-backed refresh token.** Short-lived access token, longer-lived
refresh token that is a row and can therefore be invalidated.

## Decision

ASP.NET Core Identity via `AddIdentityCore` (not `AddIdentity`, which would register cookie
authentication and make it the default scheme), plus 15-minute JWT access tokens and
7-day single-use refresh tokens with rotation and replay detection.

## Consequences

**Easier**

- Password hashing, lockout, normalization and role storage are solved and maintained.
- Authorizing a request is a signature check — no database read, so any instance can serve
  any request.
- Email confirmation, two-factor and external logins can be enabled later without a
  migration, because the columns already exist.
- Rotation makes a stolen refresh token detectable, and detection invalidates the whole
  chain.

**Harder**

- **`Modules.Users.Domain` references `Microsoft.AspNetCore.Identity.EntityFrameworkCore`.**
  A strict reading of [ADR-0002](0002-clean-architecture-vertical-slices.md) forbids that.
  The alternative — a parallel domain `User` mapped to an Identity persistence model, in a
  module whose whole job is the Identity integration — is not worth it. The architecture
  test permits this one namespace and blocks the rest, so the exception is explicit rather
  than a slow leak.
- **An access token cannot be revoked.** Demote a user and they keep their permissions for
  up to 15 minutes. This is inherent to stateless tokens, not a bug, and it is why the
  lifetime is short. Noted in `UpdateUserRoleHandler` where it bites.
- Clients must implement the refresh flow, including the case where refresh itself fails.
- Replay detection logs out the legitimate user too. Deliberate: at that point the two
  parties are indistinguishable, and ending both sessions is safer than guessing.
- Four unused Identity tables.
- The signing key is a genuine secret. It cannot live in `appsettings.json`; in development
  the AppHost supplies it, in production a secret store must.

## Related

- [concepts/aspnet-core-identity.md](../concepts/aspnet-core-identity.md)
- [concepts/authentication-and-jwt.md](../concepts/authentication-and-jwt.md)
