using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Common.Domain.Results;
using Modules.Users.Domain.Users;
using Modules.Users.Features.Users.DeleteUser;
using Modules.Users.Tests.Unit.Configuration;
using NSubstitute;

namespace Modules.Users.Tests.Unit.Users;

/// <summary>
/// Covers <see cref="DeleteUserHandler"/>.
/// </summary>
/// <remarks>
/// The rule worth guarding here is the self-delete check. Its whole purpose is to stop an
/// organisation removing its last admin, and it is one <c>if</c> away from being deleted
/// by someone who reads it as a needless restriction.
/// </remarks>
public class DeleteUserHandlerTests
{
    private const string CallerId = "admin-1";

    private readonly UserManager<User> _userManager = IdentitySubstitutes.UserManager();

    private DeleteUserHandler CreateHandler() =>
        new(_userManager, NullLogger<DeleteUserHandler>.Instance);

    [Fact]
    public async Task HandleAsync_WithAnotherUser_DeletesThem()
    {
        var user = IdentitySubstitutes.AUser("user-2");
        _userManager.FindByIdAsync("user-2").Returns(user);
        _userManager.DeleteAsync(user).Returns(IdentityResult.Success);

        var result = await CreateHandler().HandleAsync("user-2", CallerId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await _userManager.Received(1).DeleteAsync(user);
    }

    [Fact]
    public async Task HandleAsync_WhenTheCallerIsTheTarget_ReturnsCannotDeleteSelf()
    {
        var result = await CreateHandler().HandleAsync(CallerId, CallerId, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Users.CannotDeleteSelf", result.FirstError.Code);
        Assert.Equal(ErrorType.Conflict, result.FirstError.Type);
    }

    [Fact]
    public async Task HandleAsync_WhenTheCallerIsTheTarget_DoesNotTouchTheDatabase()
    {
        await CreateHandler().HandleAsync(CallerId, CallerId, CancellationToken.None);

        // The guard has to come first. Reordering it after the lookup would still return
        // the right error for an existing account, and would silently start returning
        // NotFound to an admin whose own record had already gone.
        await _userManager.DidNotReceive().FindByIdAsync(Arg.Any<string>());
        await _userManager.DidNotReceive().DeleteAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task HandleAsync_WhenTheUserDoesNotExist_ReturnsNotFound()
    {
        _userManager.FindByIdAsync("missing").Returns((User?)null);

        var result = await CreateHandler().HandleAsync("missing", CallerId, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Users.NotFound", result.FirstError.Code);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        await _userManager.DidNotReceive().DeleteAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task HandleAsync_WhenIdentityRefusesTheDelete_ReturnsDeleteFailed()
    {
        var user = IdentitySubstitutes.AUser("user-2");
        _userManager.FindByIdAsync("user-2").Returns(user);
        _userManager.DeleteAsync(user).Returns(IdentitySubstitutes.Failed("ConcurrencyFailure", "Stale record."));

        var result = await CreateHandler().HandleAsync("user-2", CallerId, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Users.DeleteFailed", result.FirstError.Code);
        // Identity's own description is preserved rather than replaced with our wording.
        Assert.Contains("Stale record.", result.FirstError.Description, StringComparison.Ordinal);
    }
}
