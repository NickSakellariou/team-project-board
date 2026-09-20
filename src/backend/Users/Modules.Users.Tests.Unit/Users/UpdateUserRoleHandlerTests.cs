using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Common.Domain.Results;
using Modules.Users.Domain.Users;
using Modules.Users.Features.Users.UpdateUserRole;
using Modules.Users.Tests.Unit.Configuration;
using NSubstitute;

namespace Modules.Users.Tests.Unit.Users;

/// <summary>
/// Covers <see cref="UpdateUserRoleHandler"/>.
/// </summary>
/// <remarks>
/// Granting a role is privilege escalation, so this handler carries the worst failure mode
/// in the module: a user left holding two roles holds the union of their claims, which is
/// Admin, permanently. The "replaces rather than accumulates" tests exist to make that
/// regression impossible to introduce quietly.
/// </remarks>
public class UpdateUserRoleHandlerTests
{
    private const string CallerId = "admin-1";
    private const string TargetId = "user-2";

    private readonly UserManager<User> _userManager = IdentitySubstitutes.UserManager();
    private readonly RoleManager<Role> _roleManager = IdentitySubstitutes.RoleManager();

    private UpdateUserRoleHandler CreateHandler() =>
        new(_userManager, _roleManager, NullLogger<UpdateUserRoleHandler>.Instance);

    private User GivenTheTargetExists(params string[] currentRoles)
    {
        var user = IdentitySubstitutes.AUser(TargetId);

        _roleManager.RoleExistsAsync(Arg.Any<string>()).Returns(true);
        _userManager.FindByIdAsync(TargetId).Returns(user);
        _userManager.GetRolesAsync(user).Returns<IList<string>>([.. currentRoles]);
        _userManager.RemoveFromRolesAsync(user, Arg.Any<IEnumerable<string>>()).Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(user, Arg.Any<string>()).Returns(IdentityResult.Success);

        return user;
    }

    [Fact]
    public async Task HandleAsync_ForAnotherUser_ReturnsThemHoldingOnlyTheNewRole()
    {
        GivenTheTargetExists(SystemRoles.User);

        var result = await CreateHandler().HandleAsync(
            TargetId,
            CallerId,
            new UpdateUserRoleRequest(SystemRoles.Admin),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([SystemRoles.Admin], result.Value!.Roles);
    }

    [Fact]
    public async Task HandleAsync_RemovesTheExistingRoleBeforeAddingTheNewOne()
    {
        var user = GivenTheTargetExists(SystemRoles.User);

        // Recorded by hand rather than with Received.InOrder, whose un-awaited calls the
        // MA0134 analyzer rejects.
        var calls = new List<string>();
        _userManager.RemoveFromRolesAsync(user, Arg.Any<IEnumerable<string>>())
            .Returns(_ =>
            {
                calls.Add("remove");
                return IdentityResult.Success;
            });
        _userManager.AddToRoleAsync(user, Arg.Any<string>())
            .Returns(_ =>
            {
                calls.Add("add");
                return IdentityResult.Success;
            });

        await CreateHandler().HandleAsync(
            TargetId,
            CallerId,
            new UpdateUserRoleRequest(SystemRoles.Admin),
            CancellationToken.None);

        // Order matters as much as the calls themselves: adding first would leave the user
        // holding both roles for the duration, and holding both permanently if the removal
        // then failed.
        Assert.Equal(["remove", "add"], calls);
    }

    [Fact]
    public async Task HandleAsync_RemovesEveryRoleTheUserCurrentlyHolds()
    {
        var user = GivenTheTargetExists(SystemRoles.User, SystemRoles.Admin);

        await CreateHandler().HandleAsync(
            TargetId,
            CallerId,
            new UpdateUserRoleRequest(SystemRoles.User),
            CancellationToken.None);

        // Not just the first one. A user who somehow holds two roles has to come out of
        // this holding exactly the requested one.
        await _userManager.Received(1).RemoveFromRolesAsync(
            user,
            Arg.Is<IEnumerable<string>>(roles =>
                roles.SequenceEqual(new[] { SystemRoles.User, SystemRoles.Admin })));
    }

    [Fact]
    public async Task HandleAsync_WhenTheUserHoldsNoRoles_SkipsTheRemoval()
    {
        var user = GivenTheTargetExists();

        var result = await CreateHandler().HandleAsync(
            TargetId,
            CallerId,
            new UpdateUserRoleRequest(SystemRoles.Admin),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        await _userManager.DidNotReceive().RemoveFromRolesAsync(user, Arg.Any<IEnumerable<string>>());
        await _userManager.Received(1).AddToRoleAsync(user, SystemRoles.Admin);
    }

    [Fact]
    public async Task HandleAsync_WhenRemovingTheOldRoleFails_DoesNotAddTheNewOne()
    {
        var user = GivenTheTargetExists(SystemRoles.User);
        _userManager.RemoveFromRolesAsync(user, Arg.Any<IEnumerable<string>>())
            .Returns(IdentitySubstitutes.Failed("ConcurrencyFailure", "Stale record."));

        var result = await CreateHandler().HandleAsync(
            TargetId,
            CallerId,
            new UpdateUserRoleRequest(SystemRoles.Admin),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Users.RoleUpdateFailed", result.FirstError.Code);
        // Continuing past a failed removal is exactly how a user ends up holding both.
        await _userManager.DidNotReceive().AddToRoleAsync(user, Arg.Any<string>());
    }

    [Fact]
    public async Task HandleAsync_WhenAddingTheNewRoleFails_ReturnsRoleUpdateFailed()
    {
        var user = GivenTheTargetExists(SystemRoles.User);
        _userManager.AddToRoleAsync(user, Arg.Any<string>())
            .Returns(IdentitySubstitutes.Failed("UserAlreadyInRole", "Already in role."));

        var result = await CreateHandler().HandleAsync(
            TargetId,
            CallerId,
            new UpdateUserRoleRequest(SystemRoles.Admin),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Users.RoleUpdateFailed", result.FirstError.Code);
        Assert.Equal(ErrorType.Failure, result.FirstError.Type);
    }

    [Fact]
    public async Task HandleAsync_WhenTheCallerIsTheTarget_ReturnsCannotChangeOwnRole()
    {
        var result = await CreateHandler().HandleAsync(
            CallerId,
            CallerId,
            new UpdateUserRoleRequest(SystemRoles.User),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Users.CannotChangeOwnRole", result.FirstError.Code);
        Assert.Equal(ErrorType.Conflict, result.FirstError.Type);
    }

    [Fact]
    public async Task HandleAsync_WhenTheCallerIsTheTarget_ChangesNothing()
    {
        await CreateHandler().HandleAsync(
            CallerId,
            CallerId,
            new UpdateUserRoleRequest(SystemRoles.User),
            CancellationToken.None);

        // The last admin demoting themselves leaves nobody able to promote anyone, so this
        // has to stop before any write — and before the role is even looked up.
        await _roleManager.DidNotReceive().RoleExistsAsync(Arg.Any<string>());
        await _userManager.DidNotReceive().RemoveFromRolesAsync(Arg.Any<User>(), Arg.Any<IEnumerable<string>>());
        await _userManager.DidNotReceive().AddToRoleAsync(Arg.Any<User>(), Arg.Any<string>());
    }

    [Fact]
    public async Task HandleAsync_WithARoleThatDoesNotExist_ReturnsRoleNotFound()
    {
        _roleManager.RoleExistsAsync("Superuser").Returns(false);

        var result = await CreateHandler().HandleAsync(
            TargetId,
            CallerId,
            new UpdateUserRoleRequest("Superuser"),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Users.RoleNotFound", result.FirstError.Code);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        // Checked before anything is written, so an unknown role cannot strip an existing one.
        await _userManager.DidNotReceive().RemoveFromRolesAsync(Arg.Any<User>(), Arg.Any<IEnumerable<string>>());
    }

    [Fact]
    public async Task HandleAsync_WhenTheUserDoesNotExist_ReturnsNotFound()
    {
        _roleManager.RoleExistsAsync(Arg.Any<string>()).Returns(true);
        _userManager.FindByIdAsync("missing").Returns((User?)null);

        var result = await CreateHandler().HandleAsync(
            "missing",
            CallerId,
            new UpdateUserRoleRequest(SystemRoles.Admin),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Users.NotFound", result.FirstError.Code);
    }
}
