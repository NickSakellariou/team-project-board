using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Common.Domain.Results;
using Modules.Users.Domain.Users;
using Modules.Users.Features.Users.RegisterUser;
using Modules.Users.Tests.Unit.Configuration;
using NSubstitute;

namespace Modules.Users.Tests.Unit.Users;

/// <summary>
/// Covers <see cref="RegisterUserHandler"/>.
/// </summary>
/// <remarks>
/// The integration suite proves a registered account comes back holding only the User
/// role. What it cannot reach is the rollback: role assignment failing after the account
/// was already created. That path leaves a user with no role at all if the compensating
/// delete is ever dropped, and it only fires when the application is misconfigured — so it
/// would not be noticed until it mattered.
/// </remarks>
public class RegisterUserHandlerTests
{
    private readonly UserManager<User> _userManager = IdentitySubstitutes.UserManager();

    private RegisterUserHandler CreateHandler() =>
        new(_userManager, NullLogger<RegisterUserHandler>.Instance);

    private static RegisterUserRequest ARequest() =>
        new("nick@example.com", "Password1", "Nick");

    [Fact]
    public async Task HandleAsync_WithAValidRequest_ReturnsTheNewUser()
    {
        _userManager.CreateAsync(Arg.Any<User>(), Arg.Any<string>()).Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<User>(), Arg.Any<string>()).Returns(IdentityResult.Success);

        var result = await CreateHandler().HandleAsync(ARequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("nick@example.com", result.Value!.Email);
        Assert.Equal("Nick", result.Value.DisplayName);
        Assert.NotEmpty(result.Value.Id);
    }

    [Fact]
    public async Task HandleAsync_AlwaysGrantsTheDefaultRole_AndNothingElse()
    {
        _userManager.CreateAsync(Arg.Any<User>(), Arg.Any<string>()).Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<User>(), Arg.Any<string>()).Returns(IdentityResult.Success);

        var result = await CreateHandler().HandleAsync(ARequest(), CancellationToken.None);

        // Self-registration must never be a route to Admin, whatever the request contains.
        await _userManager.Received(1).AddToRoleAsync(Arg.Any<User>(), SystemRoles.User);
        await _userManager.DidNotReceive().AddToRoleAsync(Arg.Any<User>(), SystemRoles.Admin);
        Assert.Equal([SystemRoles.User], result.Value!.Roles);
    }

    [Fact]
    public async Task HandleAsync_SetsTheUserNameToTheEmail()
    {
        User? created = null;
        _userManager.CreateAsync(Arg.Do<User>(user => created = user), Arg.Any<string>())
            .Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<User>(), Arg.Any<string>()).Returns(IdentityResult.Success);

        await CreateHandler().HandleAsync(ARequest(), CancellationToken.None);

        // Identity treats UserName as the login identifier and validates it. Leaving it
        // unset fails registration outright, which is a confusing way to find out.
        Assert.NotNull(created);
        Assert.Equal("nick@example.com", created.UserName);
        Assert.Equal("nick@example.com", created.Email);
    }

    [Fact]
    public async Task HandleAsync_PassesThePasswordToIdentity_RatherThanStoringIt()
    {
        User? created = null;
        _userManager.CreateAsync(Arg.Do<User>(user => created = user), "Password1")
            .Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<User>(), Arg.Any<string>()).Returns(IdentityResult.Success);

        await CreateHandler().HandleAsync(ARequest(), CancellationToken.None);

        // Hashing belongs to Identity. The entity must reach CreateAsync with no password
        // set on it at all.
        await _userManager.Received(1).CreateAsync(Arg.Any<User>(), "Password1");
        Assert.NotNull(created);
        Assert.Null(created.PasswordHash);
    }

    [Fact]
    public async Task HandleAsync_WhenIdentityRejectsTheAccount_ReturnsRegistrationFailed()
    {
        _userManager.CreateAsync(Arg.Any<User>(), Arg.Any<string>())
            .Returns(IdentitySubstitutes.Failed("DuplicateEmail", "Email 'nick@example.com' is already taken."));

        var result = await CreateHandler().HandleAsync(ARequest(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Users.RegistrationFailed", result.FirstError.Code);
        // Conflict, so the endpoint answers 409 rather than a generic 400.
        Assert.Equal(ErrorType.Conflict, result.FirstError.Type);
        Assert.Contains("already taken", result.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_WhenTheAccountIsRejected_DoesNotAssignARole()
    {
        _userManager.CreateAsync(Arg.Any<User>(), Arg.Any<string>())
            .Returns(IdentitySubstitutes.Failed("PasswordTooShort", "Password is too short."));

        await CreateHandler().HandleAsync(ARequest(), CancellationToken.None);

        await _userManager.DidNotReceive().AddToRoleAsync(Arg.Any<User>(), Arg.Any<string>());
    }

    [Fact]
    public async Task HandleAsync_WhenTheDefaultRoleCannotBeAssigned_RollsBackTheAccount()
    {
        User? created = null;
        _userManager.CreateAsync(Arg.Do<User>(user => created = user), Arg.Any<string>())
            .Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<User>(), Arg.Any<string>())
            .Returns(IdentitySubstitutes.Failed("RoleNotFound", "Role 'User' was not found."));

        var result = await CreateHandler().HandleAsync(ARequest(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Users.RegistrationFailed", result.FirstError.Code);

        // Registration is all-or-nothing. Without the compensating delete the account
        // survives with no role, which means it can log in and then fail every
        // authorization check — a state nothing else in the module knows how to repair.
        Assert.NotNull(created);
        await _userManager.Received(1).DeleteAsync(created);
    }
}
