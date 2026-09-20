using Microsoft.AspNetCore.Identity;
using Modules.Common.Domain.Results;
using Modules.Users.Domain.Users;
using Modules.Users.Features.Users.GetCurrentUser;
using Modules.Users.Features.Users.GetUserById;
using Modules.Users.Tests.Unit.Configuration;
using NSubstitute;

namespace Modules.Users.Tests.Unit.Users;

/// <summary>
/// Covers <see cref="GetCurrentUserHandler"/>.
/// </summary>
/// <remarks>
/// Two classes share this file because the handlers are a matched pair: the same read,
/// differing only in whose id arrives. Keeping them together makes it obvious when one
/// gains behaviour the other did not.
/// </remarks>
public class GetCurrentUserHandlerTests
{
    private readonly UserManager<User> _userManager = IdentitySubstitutes.UserManager();

    private GetCurrentUserHandler CreateHandler() => new(_userManager);

    [Fact]
    public async Task HandleAsync_ForAnExistingUser_ReturnsTheirProfileAndRoles()
    {
        var user = IdentitySubstitutes.AUser("user-1", "Nick");
        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.GetRolesAsync(user).Returns<IList<string>>([SystemRoles.User]);

        var result = await CreateHandler().HandleAsync("user-1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("user-1", result.Value!.Id);
        Assert.Equal("Nick", result.Value.DisplayName);
        Assert.Equal([SystemRoles.User], result.Value.Roles);
    }

    [Fact]
    public async Task HandleAsync_ReadsTheStore_RatherThanTrustingTheToken()
    {
        var user = IdentitySubstitutes.AUser("user-1", "Renamed Since The Token Was Issued");
        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.GetRolesAsync(user).Returns<IList<string>>([SystemRoles.User]);

        var result = await CreateHandler().HandleAsync("user-1", CancellationToken.None);

        // The access token already carries a display name, up to AccessTokenMinutes stale.
        // Answering from it would show a user their old name after they just changed it.
        Assert.Equal("Renamed Since The Token Was Issued", result.Value!.DisplayName);
        await _userManager.Received(1).FindByIdAsync("user-1");
    }

    [Fact]
    public async Task HandleAsync_WhenTheAccountHasBeenDeleted_ReturnsNotFound()
    {
        _userManager.FindByIdAsync("user-1").Returns((User?)null);

        var result = await CreateHandler().HandleAsync("user-1", CancellationToken.None);

        // Reachable in production: a still-valid token belonging to a since-deleted account.
        Assert.True(result.IsError);
        Assert.Equal("Users.NotFound", result.FirstError.Code);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }
}

/// <summary>
/// Covers <see cref="GetUserByIdHandler"/>.
/// </summary>
public class GetUserByIdHandlerTests
{
    private readonly UserManager<User> _userManager = IdentitySubstitutes.UserManager();

    private GetUserByIdHandler CreateHandler() => new(_userManager);

    [Fact]
    public async Task HandleAsync_ForAnExistingUser_ReturnsTheirProfileAndRoles()
    {
        var user = IdentitySubstitutes.AUser("user-2", "Someone Else");
        _userManager.FindByIdAsync("user-2").Returns(user);
        _userManager.GetRolesAsync(user).Returns<IList<string>>([SystemRoles.Admin]);

        var result = await CreateHandler().HandleAsync("user-2", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("user-2", result.Value!.Id);
        Assert.Equal("Someone Else", result.Value.DisplayName);
        Assert.Equal([SystemRoles.Admin], result.Value.Roles);
    }

    [Fact]
    public async Task HandleAsync_ForAnUnknownId_ReturnsNotFound()
    {
        _userManager.FindByIdAsync("missing").Returns((User?)null);

        var result = await CreateHandler().HandleAsync("missing", CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Users.NotFound", result.FirstError.Code);
        Assert.Contains("missing", result.FirstError.Description, StringComparison.Ordinal);
    }
}
