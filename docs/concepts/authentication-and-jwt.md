# Authentication, JWTs and Refresh Tokens

## Authentication vs authorization

Two different questions, often conflated:

- **Authentication** — *who are you?* Produces an identity. Failing it is **401 Unauthorized**
  (a badly named status; it means "unauthenticated").
- **Authorization** — *are you allowed to do this?* Takes an identity and a resource, and
  decides. Failing it is **403 Forbidden**.

The distinction shows up directly in the middleware pipeline, and the order is not
negotiable:

```csharp
app.UseAuthentication();   // reads the token, populates HttpContext.User
app.UseAuthorization();    // checks that user against the endpoint's policy
```

Reversed, authorization runs against an anonymous user and every protected endpoint
returns 401 regardless of the token.

Getting the status codes right matters to the frontend: a 401 means "log in again", a 403
means "you are logged in, this is not for you". Send 401 for the second and you bounce a
signed-in user to a login screen that will not help them.

## What a JWT actually is

Three base64url-encoded parts separated by dots:

```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9 . eyJzdWIiOiJiY2M0N2I5MiIsImV4cCI6MTc2 . 4pcPfQdbT8s...
        header                                    payload                        signature
```

**Header** — the algorithm: `{"alg":"HS256","typ":"JWT"}`.

**Payload** — the claims. Ours carries:

| Claim | Meaning |
|---|---|
| `sub` | Subject — the user id. A registered claim from the JWT spec. |
| `jti` | JWT id — unique per token. The refresh-token row points at it. |
| `exp` | Expiry, as a Unix timestamp. |
| `nbf` | Not-before. |
| `iss` / `aud` | Issuer and audience. |
| `email` | Convenience. |
| `userid` | Our own copy of the id (see below). |
| `role` | The user's system role. |
| `users:read`, `users:update`, … | The permissions their role carries. |

**Signature** — `HMACSHA256(base64(header) + "." + base64(payload), secret)`.

### The single most important thing to understand

**The payload is encoded, not encrypted.** Anyone holding the token can read every claim
in it — paste one into <https://jwt.io> and it is all there. The signature does not hide
anything; it only proves the content has not been *changed*.

So: never put anything secret in a JWT. No password hashes, no personal data you would not
hand to the bearer.

What the signature does guarantee is integrity. Change one byte of the payload and the
signature no longer matches, because producing a valid signature requires the secret. That
is why a client cannot simply edit `"role":"User"` into `"role":"Admin"`.

### Why we duplicate the user id in a `userid` claim

ASP.NET Core's JWT handler, for backwards-compatibility reasons, remaps the standard `sub`
claim to the WS-Federation URI
`http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier`. Reading `sub`
directly therefore does not work the way you would expect. Rather than depend on that
remapping, `AuthenticationService` writes our own `userid` claim and
`ClaimsPrincipalExtensions.GetUserId()` reads it.

## Why the signing key is the whole security model

`AuthConfiguration:Key` is a **symmetric** secret — the same value signs and verifies.
Anyone holding it can mint a token this API will accept, for any user, with any role.

That is why it is not in `appsettings.json`. Committed configuration is deployed
configuration, and a key in the repository is a key an attacker can read. In development
it is injected by the AppHost (a program that is never deployed); in production it must
come from an environment variable or a secret store.

If it ever leaks: rotate it. Every existing token immediately becomes invalid, which is
disruptive and correct.

## What is validated on every request

From `AddJwtAuthentication`:

```csharp
ValidateIssuerSigningKey = true,   // it was signed by us, and not altered
ValidateIssuer = true,             // by this application
ValidateAudience = true,           // and intended for this application
ValidateLifetime = true,           // and has not expired
ClockSkew = TimeSpan.FromSeconds(5)
```

Issuer and audience matter when several systems share a signing key: without them, a token
minted for a different service would be accepted here.

`ClockSkew` defaults to **five minutes** of leniency for clock differences between
machines. That silently extends every token's life by five minutes. With NTP everywhere,
five seconds is plenty, and it keeps the expiry meaning what it says.

## The problem JWTs create, and refresh tokens solve

A JWT is validated by checking a signature. No database lookup, no shared session store —
which is exactly what makes it fast and lets any instance serve any request.

It is also what makes it **impossible to revoke.** There is nowhere to delete it from. Once
issued, a token is valid until `exp`, full stop:

- A user is fired. Their token still works.
- An admin demotes someone. They keep admin permissions.
- A token is stolen. It works until it expires.

You cannot fix this without giving up the property that made JWTs attractive — a
revocation check is a database read on every request, which is a session, with extra steps.

So the standard answer is to **bound the damage instead of preventing it**: make access
tokens short-lived, and add a second credential to renew them.

| | Access token (JWT) | Refresh token |
|---|---|---|
| Lifetime | 15 minutes | 7 days |
| Where checked | Signature only | Database row |
| Revocable? | No | Yes |
| Sent with | Every request | Only to `/api/users/refresh` |

The worst case for a stolen access token is now 15 minutes. The refresh token lives longer
but is a row we can invalidate, and it is transmitted far less often.

**This is the trade that explains `AccessTokenMinutes`.** Shorter is safer and means more
refresh round trips. Fifteen minutes is a common middle. It is also why demoting a user
does not take effect instantly — noted directly in `UpdateUserRoleHandler`.

## Rotation and replay detection

Each refresh **consumes** its token and issues a new one. From `IssueTokensAsync`:

```csharp
previousRefreshToken.Used = true;         // spent, not deleted
dbContext.RefreshTokens.Add(refreshToken); // a fresh one
await dbContext.SaveChangesAsync();        // both in one transaction
```

Two details worth dwelling on.

**Why mark it used rather than delete it.** A deleted token and a token that never existed
are indistinguishable. A token still present but marked `Used` is *evidence*: someone
presented a credential that was already spent.

**Why that evidence matters.** Consider a stolen refresh token:

1. Attacker steals the refresh token.
2. Attacker refreshes. They get a new pair. The old token is now spent.
3. The real user's client tries to refresh with the token it still holds — the spent one.
4. The server sees a used token come back.

The server cannot tell which party is legitimate. So it assumes the worst and invalidates
**every** refresh token for that user (`InvalidateAllTokensForUserAsync`). Both parties are
logged out. The real user signs in again, mildly annoyed; the attacker's access is over.

That is the right trade, and `Refresh_AfterAReplay_InvalidatesTheWholeChain` in the
integration tests pins the behaviour.

### Refreshing with an expired token

The access token sent to `/refresh` has usually already expired. So `GetPrincipalFromExpiredToken`
clones the validation parameters with `ValidateLifetime = false`. Everything else still
applies — signature, issuer, audience — so the token must still be one we issued.

It also pins the algorithm:

```csharp
if (!jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
    return null;
```

This blocks a classic attack. A naive validator trusts the `alg` in the token's own header;
an attacker sets `"alg":"none"`, strips the signature, and the token is accepted. Deciding
the algorithm server-side closes it off. The analyzer flags the disabled lifetime check
(CA5404) and the suppression on that method spells out why it is safe here.

## Authorization: policies over roles

`RequireRole("Admin")` bakes today's org chart into every endpoint. Add a "Support" role
that may read users but not delete them, and you edit every endpoint.

Policies name the *permission* instead:

```csharp
.RequireAuthorization(UserPolicyConsts.Read)   // "whoever may read users"
```

The chain from role to endpoint:

1. `UserSeedService` attaches `users:read` (and friends) to the Admin role → a `role_claim` row.
2. On login, `AuthenticationService` copies the user's role claims into their JWT.
3. `UsersPolicyFactory` declares a policy per permission, each a `RequireClaim`.
4. `AuthorizationConfigureOptions` collects every module's factory at startup.
5. The endpoint names the policy.

Change what an Admin may do by changing step 1. No endpoint changes.

Flattening permissions into the token at login is what makes each subsequent request a
pure signature check — no database read to authorize. The price is the staleness already
described.

### Why each module declares its own policies

`AddAuthorization(options => ...)` in `Program.cs` would put every module's permission
names in a file the host owns. `IPolicyFactory` inverts it: the module declares its own,
the host collects them without knowing what they are. The Boards module will add
`boards:*` without touching `Program.cs`.

## Where the frontend should store tokens

Not settled here, but the shape of the problem:

- **`localStorage`** — simple, and readable by any JavaScript on the page, so an XSS bug
  hands over the token.
- **An httpOnly cookie** — unreadable by JavaScript, but sent automatically, which opens
  CSRF and needs `SameSite` plus anti-forgery handling.
- **In memory only** — safest, and lost on page refresh unless the refresh token lives
  somewhere more durable.

A common compromise: access token in memory, refresh token in an httpOnly cookie. Worth
deciding deliberately in Sprint 6 rather than by default.

## Related

- [aspnet-core-identity.md](aspnet-core-identity.md) — how the password is checked before any of this
- [anatomy-of-a-slice.md](anatomy-of-a-slice.md) — the login request end to end
- [ADR-0008](../adr/0008-identity-and-jwt.md) — the decision record
