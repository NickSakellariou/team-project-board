using Microsoft.AspNetCore.Identity;
using Modules.Users.Domain.Errors;

namespace Modules.Users.Tests.Unit.Users;

/// <summary>
/// Pins the code and message of every error in <see cref="UserErrors"/>.
/// </summary>
/// <remarks>
/// <para>
/// An error code is a published contract: the API returns it in the problem-details body
/// so a client can branch on a stable identifier, and logs are filtered by it. Because
/// each code is built from <c>nameof</c>, renaming a factory method changes the contract
/// without changing a single call site. These tests turn that into a failing build.
/// </para>
/// <para>
/// They assert code and message only. The <c>ErrorType</c> — and therefore the HTTP status
/// — is covered where it is actually observable, in the integration tests.
/// </para>
/// </remarks>
public class UserErrorsTests
{
    private static readonly IdentityError[] IdentityErrors =
    [
        new() { Code = "PasswordTooShort", Description = "Passwords must be at least 8 characters." },
        new() { Code = "DuplicateEmail", Description = "Email 'nick@example.com' is already taken." }
    ];

    [Fact]
    public void NotFound_HasTheExpectedCodeAndMessage()
    {
        var error = UserErrors.NotFound("user-123");

        Assert.Equal("Users.NotFound", error.Code);
        Assert.Equal("User 'user-123' was not found.", error.Description);
    }

    [Fact]
    public void InvalidCredentials_HasTheExpectedCodeAndMessage()
    {
        var error = UserErrors.InvalidCredentials();

        Assert.Equal("Users.InvalidCredentials", error.Code);

        // Deliberately says neither "no such user" nor "wrong password". If this message
        // ever becomes specific, the login form becomes an account-enumeration oracle.
        Assert.Equal("Invalid email or password.", error.Description);
    }

    [Fact]
    public void LockedOut_HasTheExpectedCodeAndMessage()
    {
        var error = UserErrors.LockedOut();

        Assert.Equal("Users.LockedOut", error.Code);
        Assert.Equal("This account is temporarily locked. Try again later.", error.Description);
    }

    [Fact]
    public void InvalidToken_HasTheExpectedCodeAndMessage()
    {
        var error = UserErrors.InvalidToken();

        Assert.Equal("Users.InvalidToken", error.Code);
        Assert.Equal("The token is invalid or has expired.", error.Description);
    }

    [Fact]
    public void RoleNotFound_HasTheExpectedCodeAndMessage()
    {
        var error = UserErrors.RoleNotFound("Moderator");

        Assert.Equal("Users.RoleNotFound", error.Code);
        Assert.Equal("Role 'Moderator' was not found.", error.Description);
    }

    [Fact]
    public void CannotDeleteSelf_HasTheExpectedCodeAndMessage()
    {
        var error = UserErrors.CannotDeleteSelf();

        Assert.Equal("Users.CannotDeleteSelf", error.Code);
        Assert.Equal("You cannot delete your own account.", error.Description);
    }

    [Fact]
    public void CannotChangeOwnRole_HasTheExpectedCodeAndMessage()
    {
        var error = UserErrors.CannotChangeOwnRole();

        Assert.Equal("Users.CannotChangeOwnRole", error.Code);
        Assert.Equal("You cannot change your own role.", error.Description);
    }

    [Fact]
    public void RegistrationFailed_HasTheExpectedCodeAndMessage()
    {
        var error = UserErrors.RegistrationFailed(IdentityErrors);

        Assert.Equal("Users.RegistrationFailed", error.Code);
        Assert.Equal(
            "Passwords must be at least 8 characters. Email 'nick@example.com' is already taken.",
            error.Description);
    }

    [Fact]
    public void UpdateFailed_HasTheExpectedCodeAndMessage()
    {
        var error = UserErrors.UpdateFailed(IdentityErrors);

        Assert.Equal("Users.UpdateFailed", error.Code);
        Assert.Equal(
            "Passwords must be at least 8 characters. Email 'nick@example.com' is already taken.",
            error.Description);
    }

    [Fact]
    public void DeleteFailed_HasTheExpectedCodeAndMessage()
    {
        var error = UserErrors.DeleteFailed(IdentityErrors);

        Assert.Equal("Users.DeleteFailed", error.Code);
        Assert.Equal(
            "Passwords must be at least 8 characters. Email 'nick@example.com' is already taken.",
            error.Description);
    }

    [Fact]
    public void RoleUpdateFailed_HasTheExpectedCodeAndMessage()
    {
        var error = UserErrors.RoleUpdateFailed(IdentityErrors);

        Assert.Equal("Users.RoleUpdateFailed", error.Code);
        Assert.Equal(
            "Passwords must be at least 8 characters. Email 'nick@example.com' is already taken.",
            error.Description);
    }

    [Fact]
    public void AnIdentityFailure_WithNoErrors_ProducesAnEmptyMessage()
    {
        // Identity can report a failure with an empty error collection. The catalogue must
        // still produce a well-formed Error rather than throw, because the handler has
        // already decided it is returning a failure by the time it calls this.
        var error = UserErrors.UpdateFailed([]);

        Assert.Equal("Users.UpdateFailed", error.Code);
        Assert.Equal(string.Empty, error.Description);
    }
}
