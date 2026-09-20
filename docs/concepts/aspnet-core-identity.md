# ASP.NET Core Identity

## What it is

A library that handles user accounts: storing them, hashing passwords, checking
credentials, managing roles and claims, locking out accounts after repeated failures, and
generating tokens for password resets and email confirmation.

It is **not** an authentication scheme. Identity answers "is this the right password for
this user?" — it says nothing about how a browser proves who it is on the next request.
That is a separate job, and in this project it is done with JWTs; see
[authentication-and-jwt.md](authentication-and-jwt.md).

## Why it exists — why not just write a users table?

Because "store a user and check their password" is a much larger job than it looks, and
almost every part of it is a security decision where being 95% right is being wrong.

Consider what a hand-rolled version has to get right:

- **Hashing.** Not MD5, not SHA-256 — those are *fast*, which is precisely wrong for
  passwords, because fast means an attacker with the leaked table can try billions of
  guesses per second. You need a deliberately slow algorithm with a per-user salt and a
  tuned iteration count, and you need a plan for raising that count as hardware improves
  without invalidating existing passwords.
- **Comparison.** Comparing hashes with `==` leaks information through timing. It needs a
  fixed-time comparison.
- **Normalization.** Is `Nick@Example.com` the same account as `nick@example.com`? If you
  lower-case at write time you cannot display the original. If you compare case-insensitively
  in SQL you cannot use the index.
- **Enumeration.** If a wrong email returns instantly and a wrong password takes 100ms,
  your login form tells an attacker which addresses have accounts.
- **Lockout.** Counting failures, expiring the count, and doing so without letting an
  attacker lock out other people's accounts on purpose.
- **Invalidating sessions** after a password change.

Identity has all of that, reviewed by people who do this professionally, patched when
problems are found. Writing your own is a genuinely reasonable *learning* exercise and a
genuinely bad idea in anything real.

## What `IdentityUser` gives you

Our `User` class ([User.cs](../../src/backend/Users/Modules.Users.Domain/Users/User.cs)) is
nearly empty:

```csharp
public sealed class User : IdentityUser, IAuditableEntity
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
```

Everything else is inherited. The properties that matter:

| Property | What it is |
|---|---|
| `Id` | Primary key. A `string` — we store a GUID in it. |
| `UserName` / `NormalizedUserName` | The login identifier. We set it to the email. |
| `Email` / `NormalizedEmail` | The email. The normalized copy is upper-cased. |
| `PasswordHash` | The hash — never the password. Format below. |
| `SecurityStamp` | Random value, regenerated whenever credentials change. |
| `ConcurrencyStamp` | Optimistic concurrency guard. |
| `EmailConfirmed` | Whether the address was verified. |
| `LockoutEnd`, `AccessFailedCount`, `LockoutEnabled` | Brute-force protection. |
| `PhoneNumber`, `PhoneNumberConfirmed`, `TwoFactorEnabled` | Unused so far. |

### Why the normalized duplicates

`NormalizedEmail` holds `NICK@EXAMPLE.COM`. Two reasons it exists as a separate column
rather than being computed at query time:

1. **Indexing.** `WHERE UPPER(email) = 'NICK@EXAMPLE.COM'` cannot use a plain index on
   `email`. `WHERE normalized_email = 'NICK@EXAMPLE.COM'` can.
2. **Uniqueness.** A unique index on `normalized_email` makes the *database* reject a
   duplicate that differs only in case — so two simultaneous registrations cannot both
   pass an application-level check and both commit.

`UserConfiguration` adds that unique index explicitly, because Identity's default index on
`NormalizedEmail` is not unique.

### Why the security stamp

It answers "should existing sessions survive this change?". When a password changes,
Identity regenerates the stamp. Any authentication scheme that validates the stamp will
then reject sessions issued before the change — which is how "log out everywhere" works.

We do not currently validate it, because our JWTs are stateless and short-lived. Worth
knowing it is there, and worth knowing it is the hook if "sign out all devices" is ever
needed.

### Why the concurrency stamp

A random value that changes on every update. EF Core is configured to include it in the
`WHERE` clause of an UPDATE, so:

- Two admins load the same user.
- Both edit and save.
- The first UPDATE succeeds and changes the stamp.
- The second matches zero rows, because the stamp it remembers is stale, and EF throws
  rather than silently discarding the first admin's change.

Without it, last write wins and the earlier change vanishes with no error.

## How password hashing actually works

`PasswordHash` is a base64 string that decodes to a structured blob:

```
[version byte][PRF id][iteration count][salt length][salt][subkey]
```

The default (`version 3`) is **PBKDF2 with HMAC-SHA256, a 128-bit random salt, and a
subkey of 256 bits**, run over many iterations.

Three things are doing work here:

- **The salt** is random *per user*. Two people with the same password get different
  hashes, so an attacker cannot spot duplicates, and precomputed rainbow tables are
  useless.
- **The iteration count** makes each guess deliberately expensive. Verifying one password
  takes tens of milliseconds — imperceptible to a user logging in, devastating to someone
  trying a dictionary.
- **The version byte** means the format can change. When the iteration count is raised,
  old hashes still verify under their recorded parameters, and Identity can rehash on the
  next successful login. Without it, raising the count would mean invalidating every
  password in the database.

This is exactly the sort of detail a hand-rolled implementation gets wrong on the first
attempt and cannot fix later without a migration nobody wants to plan.

## The seven tables

`IdentityDbContext<User, Role, string>` maps seven entities. We renamed them in
[UsersDbContext.cs](../../src/backend/Users/Modules.Users.Infrastructure/Database/UsersDbContext.cs)
from Identity's `AspNetUsers` defaults to plain snake_case:

| Table | Purpose | Do we use it? |
|---|---|---|
| `users.user` | The accounts | Yes |
| `users.role` | System roles (Admin, User) | Yes |
| `users.user_role` | Which user has which role | Yes |
| `users.role_claim` | Permissions attached to a role | **Yes — this is where `users:read` etc. live** |
| `users.user_claim` | Permissions attached to one user directly | Not yet |
| `users.user_login` | External providers ("Sign in with Google") | No |
| `users.user_token` | Identity's own password-reset and 2FA tokens | No |

The unused three cost nothing empty, and adding them later would be a migration. Note that
`user_token` is unrelated to our `refresh_token` table despite the similar name — Identity's
tokens are for password resets, ours are for renewing JWTs.

### Roles vs claims, and why we authorize on claims

A **role** is who you are: `Admin`. A **claim** is what you may do: `users:delete`.

Roles alone push the org chart into the code:

```csharp
.RequireRole("Admin")   // introduce a Support role and every endpoint needs editing
```

Claims let the endpoint state what it needs, and leave who has it as data:

```csharp
.RequireAuthorization(UserPolicyConsts.Delete)   // "whoever can delete users"
```

The link between them is `role_claim`. `UserSeedService` attaches the four `users:*` claims
to the Admin role, and `AuthenticationService` copies a user's role claims into their JWT
at login. Change what an Admin may do by changing the seed — no endpoint is touched.

## The three managers

| Service | Job |
|---|---|
| `UserManager<User>` | Create, find, update, delete users. Hash and verify passwords. Manage a user's roles and claims. Lockout. |
| `SignInManager<User>` | The full sign-in flow, including cookies and two-factor. **We do not use it** — we verify with `UserManager.CheckPasswordAsync` and issue a JWT ourselves. |
| `RoleManager<Role>` | Create and find roles; read and write their claims. |

## `AddIdentityCore` vs `AddIdentity`

We call `AddIdentityCore`. This matters:

```csharp
services.AddIdentityCore<User>(options => { ... })
    .AddRoles<Role>()
    .AddEntityFrameworkStores<UsersDbContext>()
    .AddDefaultTokenProviders();
```

`AddIdentity` would additionally register **cookie authentication and make it the default
scheme.** For a server-rendered Razor app that is exactly right. For a JSON API consumed
by React it is actively harmful: an unauthenticated request would get a `302` redirect to
`/Account/Login` instead of a `401`, and the JWT scheme configured in
`AddCoreInfrastructure` would no longer be the default.

`AddIdentityCore` gives the user store, password hashing and validation, and leaves
authentication alone.

## What we configured, and why

From [UsersInfrastructureDependencyInjection.cs](../../src/backend/Users/Modules.Users.Infrastructure/UsersInfrastructureDependencyInjection.cs):

```csharp
options.Password.RequiredLength = 8;
options.Password.RequireNonAlphanumeric = false;   // deliberate
options.User.RequireUniqueEmail = true;
options.Lockout.MaxFailedAccessAttempts = 5;
options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
```

`RequireNonAlphanumeric = false` is the interesting one. Character-class rules feel strict
but mostly push people toward `Password1!` — predictable, and no harder to crack. Length
does far more work. Modern guidance (NIST SP 800-63B) says exactly this: require length,
drop composition rules.

## What we deliberately are not using

- **`SignInManager.PasswordSignInAsync`** — it issues a cookie. We want a token.
- **Email confirmation** — no mail sending yet. `EmailConfirmed` exists for when there is.
- **Two-factor** — the columns are there; the flow is not built.
- **External logins** — `user_login` is the table it would use.

None of these need a migration to adopt later, which is much of the point of accepting the
schema wholesale.

## The cost of this choice

Honesty about the trade-off: `Modules.Users.Domain` references
`Microsoft.AspNetCore.Identity.EntityFrameworkCore`. A strict Clean Architecture forbids
that — the domain should not know a framework exists.

We accepted it because the alternative is a parallel `User` domain entity plus mapping to
and from an Identity persistence model, in a module whose entire job is being the Identity
integration. The architecture test in `ModuleBoundaryTests` permits this one namespace and
blocks the rest, so the exception is explicit rather than a slow leak. Recorded in
[ADR-0008](../adr/0008-identity-and-jwt.md).

## Related

- [authentication-and-jwt.md](authentication-and-jwt.md) — what happens *after* the password checks out
- [ef-core-basics.md](ef-core-basics.md) — how these entities reach Postgres
- [anatomy-of-a-slice.md](anatomy-of-a-slice.md) — `UserManager.CreateAsync` in context
