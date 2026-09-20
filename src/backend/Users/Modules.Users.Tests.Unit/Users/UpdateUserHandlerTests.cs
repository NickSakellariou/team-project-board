using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Common.Domain.Results;
using Modules.Users.Domain.Users;
using Modules.Users.Features.Users.UpdateUser;
using Modules.Users.Tests.Unit.Configuration;
using NSubstitute;

namespace Modules.Users.Tests.Unit.Users;

/// <summary>
/// Covers <see cref="UpdateUserHandler"/>.
/// </summary>
public class UpdateUserHandlerTests
{
    private readonly UserManager<User> _userManager = IdentitySubstitutes.UserManager();

    private UpdateUserHandler CreateHandler() =>
        new(_userManager, NullLogger<UpdateUserHandler>.Instance);

    [Fact]
    public async Task HandleAsync_WithAValidRequest_ChangesTheDisplayName()
    {
        var user = IdentitySubstitutes.AUser("user-1", "Old Name");
        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        _userManager.GetRolesAsync(user).Returns<IList<string>>([SystemRoles.User]);

        var result = await CreateHandler()
            .HandleAsync("user-1", new UpdateUserRequest("New Name"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", result.Value!.DisplayName);
        Assert.Equal("New Name", user.DisplayName);
        await _userManager.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task HandleAsync_ReturnsTheRolesTheUserActuallyHolds()
    {
        var user = IdentitySubstitutes.AUser("user-1");
        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        _userManager.GetRolesAsync(user).Returns<IList<string>>([SystemRoles.Admin]);

        var result = await CreateHandler()
            .HandleAsync("user-1", new UpdateUserRequest("New Name"), CancellationToken.None);

        // A profile edit must not quietly re-state the user's roles from anywhere but the
        // store — this response is what a client caches as the current user.
        Assert.Equal([SystemRoles.Admin], result.Value!.Roles);
    }

    [Fact]
    public async Task HandleAsync_WhenTheUserDoesNotExist_ReturnsNotFound()
    {
        _userManager.FindByIdAsync("missing").Returns((User?)null);

        var result = await CreateHandler()
            .HandleAsync("missing", new UpdateUserRequest("New Name"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Users.NotFound", result.FirstError.Code);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        await _userManager.DidNotReceive().UpdateAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task HandleAsync_WhenIdentityRefusesTheUpdate_ReturnsUpdateFailed()
    {
        var user = IdentitySubstitutes.AUser("user-1");
        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.UpdateAsync(user)
            .Returns(IdentitySubstitutes.Failed("ConcurrencyFailure", "The record was modified."));

        var result = await CreateHandler()
            .HandleAsync("user-1", new UpdateUserRequest("New Name"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Users.UpdateFailed", result.FirstError.Code);
        // A concurrency clash is the failure this path exists for: two simultaneous edits
        // are detected rather than silently last-write-wins.
        Assert.Contains("modified", result.FirstError.Description, StringComparison.Ordinal);
    }
}
