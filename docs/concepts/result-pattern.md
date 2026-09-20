# The Result Pattern

## What it is

Every handler in this solution returns `Result<T>` — either a value, or one or more
`Error`s, never both and never neither.

```csharp
Task<Result<UserResponse>> HandleAsync(RegisterUserRequest request, CancellationToken ct);
```

Failure is part of the signature. Contrast:

```csharp
Task<UserResponse> HandleAsync(RegisterUserRequest request, CancellationToken ct);
```

This says the method returns a user. It does not say it might throw
`EmailAlreadyTakenException`. The only way to find out is to read the implementation, and
every implementation it calls.

## Why not exceptions?

Exceptions are excellent for the *exceptional*: the database is unreachable, a null slipped
through, a bug. They are a poor fit for outcomes you expect.

"This email is already registered" is not exceptional. It is a normal thing that happens
several times a day and that the code should handle deliberately. Four reasons to model it
as a value:

**It is invisible in the signature.** Nothing tells a caller which exceptions to expect.
C# has no checked exceptions, so the compiler will not help either.

**It is easy to swallow.** A `catch (Exception)` somewhere up the stack turns a specific
business outcome into a generic 500.

**It jumps.** An exception unwinds to whichever frame happens to catch it, which may be
far from where the decision belongs.

**It is expensive.** Throwing captures a stack trace and unwinds — orders of magnitude
slower than returning. Irrelevant once, significant on a path that fails routinely.

Exceptions are still used here. `GlobalExceptionHandler` catches them and returns a 500,
which is right: anything reaching it is a bug or infrastructure being down, and neither is
something a handler should be branching on.

## How it reads in practice

The obvious objection to result types is that they make code verbose. The implicit
conversions in `Result.ImplicitConverters.cs` are what avoid that:

```csharp
public static implicit operator Result<TValue>(TValue value) => new(value);
public static implicit operator Result<TValue>(Error error) => new(error);
```

So a handler body reads as ordinary code:

```csharp
if (!createResult.Succeeded)
{
    return UserErrors.RegistrationFailed(createResult.Errors);   // → failed Result
}

return new UserResponse(user.Id, user.Email!, user.DisplayName, [SystemRoles.User]);  // → success
```

Neither line mentions `Result<T>`. Without the conversions every return would need
`Result<UserResponse>.FromError(...)` and the pattern would feel like a tax — which is
usually why teams abandon it.

## Error

```csharp
public readonly record struct Error
{
    public string Code { get; }          // "Users.NotFound"
    public string Description { get; }   // "User 'abc' was not found."
    public ErrorType Type { get; }       // NotFound
}
```

A `readonly record struct`: a value type, so creating one allocates nothing on the heap,
and immutable, so it cannot be altered after the fact.

**`Code` is for machines, `Description` is for humans.** The API returns errors keyed by
code, so a client branches on `Users.RegistrationFailed` rather than string-matching prose
that might be reworded or translated tomorrow.

**`Type` is how the domain classifies failure without knowing about HTTP.** The mapping to
status codes happens in exactly one place, `EndpointResultsExtensions.ToProblem()`:

| ErrorType | HTTP |
|---|---|
| `Validation`, `Failure`, `Custom` | 400 |
| `Unauthorized` | 401 |
| `Forbidden` | 403 |
| `NotFound` | 404 |
| `Conflict` | 409 |
| `Unexpected` | 500 |

Because the domain never names a status code, the same handler could sit behind a SignalR
hub or a background job unchanged. Change the mapping in that one file and every endpoint
follows.

## Errors are declared per module, in one file

`UserErrors` is a static class of factory methods:

```csharp
public static Error NotFound(string userId) =>
    Error.NotFound($"{Prefix}.{nameof(NotFound)}", $"User '{userId}' was not found.");
```

Three reasons this beats constructing errors inline:

- The module's **entire failure surface is one file** you can read in a minute.
- Codes stay consistent — always `Users.Something`, never a typo.
- Reworded messages change in one place.

`nameof(NotFound)` in the code means renaming the method renames the code, so the two
cannot drift.

## The guard rails

```csharp
public TValue? Value =>
    IsError
        ? throw new InvalidOperationException("Value cannot be read when the result has errors...")
        : _value;
```

Reading `Value` on a failed result throws. That may look like reintroducing exceptions, but
it is different in kind: this fires only when the *calling code* is wrong — someone forgot
to check `IsError`. Better an immediate, obvious failure than a silent null surfacing three
layers away.

Likewise, constructing a failed result with an empty error list throws. A "failure" with no
errors would satisfy neither `IsSuccess` nor `IsError` meaningfully, so it must be
impossible to build. `ResultTests` pins both.

## `Result<Success>`

For commands with nothing to return — deleting a user — the type is `Result<Success>`,
where `Success` is an empty struct:

```csharp
public readonly record struct Success;
```

The alternative, a non-generic `Result`, would mean two parallel types and two sets of
extension methods. One empty struct costs nothing at run time and keeps everything on one
type.

## What we are not doing

Full functional-style result handling has `Map`, `Bind`, `Match` and railway-oriented
chaining:

```csharp
return await GetUser(id)
    .Bind(ValidatePermissions)
    .Map(ToResponse);
```

Elegant, and a real barrier for anyone not already fluent in it. Our handlers use plain
`if (result.IsError) return ...` instead. More lines, readable by anyone.

The combinators can be added later if a handler ever gets long enough to want them.

## Related

- [anatomy-of-a-slice.md](anatomy-of-a-slice.md) — where the result is created and consumed
- [ADR-0004](../adr/0004-result-pattern.md) — the decision record
