# 0004. `Result<T>` for business errors instead of exceptions

**Status:** Accepted
**Date:** 2026-09-13

## Context

Handlers fail in expected ways: an email is already registered, a user does not exist, a
refresh token was already spent. These have to reach the client as specific status codes
with machine-readable identifiers.

They are also *expected* — normal outcomes, not emergencies.

## Options considered

**Exceptions.** `throw new EmailAlreadyTakenException()`, caught by middleware and mapped to
a status code. Familiar and terse. But the failure is invisible in the signature — C# has no
checked exceptions, so nothing tells a caller what to expect. A broad `catch` upstream turns
a specific outcome into a generic 500. And throwing captures a stack trace and unwinds,
which is orders of magnitude slower than returning — irrelevant once, significant on a path
that fails routinely.

**Nullable returns.** `Task<UserResponse?>`, where null means failure. Cheap, and carries no
information about *why*, so the endpoint cannot choose between 404 and 409.

**A result type.** `Task<Result<UserResponse>>` — either the value or a list of `Error`s,
never both and never neither.

## Decision

`Result<T>`, returned by every handler. Errors are declared per module in one static class
(`UserErrors`) and carry a code, a description, and a type that exactly one place —
`EndpointResultsExtensions.ToProblem()` — maps to a status code.

Exceptions remain for genuine faults; `GlobalExceptionHandler` turns those into 500s.

## Consequences

**Easier**

- Failure is in the signature: a caller can see from the return type that this can fail.
- The domain never mentions HTTP, so a handler can be reused behind a SignalR hub or a
  background job unchanged — which Sprint 5 will want.
- A module's entire failure surface is one readable file, with consistent error codes.
- No stack-trace cost on a routinely failing path.

**Harder**

- Every call site must check `IsError`. Forgetting throws on `Value` access — deliberately
  loud, but still a runtime failure rather than a compile-time one.
- It reads as ceremony to anyone used to exceptions, until the implicit conversions are
  understood. Without those conversions the pattern is genuinely painful, which is why they
  exist.
- Two error-handling styles coexist: results for expected failures, exceptions for faults.
  Where the line falls is a judgement call each time.
- Identity's `IdentityResult` must be translated into ours at every boundary.

## Related

- [concepts/result-pattern.md](../concepts/result-pattern.md)
