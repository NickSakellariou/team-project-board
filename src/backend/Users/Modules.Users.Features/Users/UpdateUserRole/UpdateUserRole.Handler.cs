using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Modules.Common.Domain.Handlers;
using Modules.Common.Domain.Results;
using Modules.Users.Domain.Errors;
using Modules.Users.Domain.Users;
using Modules.Users.Features.Users.Shared;

namespace Modules.Users.Features.Users.UpdateUserRole;

/// <summary>Handles <see cref="UpdateUserRoleRequest"/>.</summary>
internal interface IUpdateUserRoleHandler : IHandler
{
    /// <summary>Replaces the user's system role.</summary>
    Task<Result<UserResponse>> HandleAsync(
        string userId,
        string callerId,
        UpdateUserRoleRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Replaces a user's system role with the requested one.
/// </summary>
/// <remarks>
/// Business rules: the role must exist; a user holds exactly one system role, so the new
/// one replaces the old; and an admin may not change their own role, which would let the
/// last admin demote themselves and leave nobody able to promote anyone.
/// </remarks>
internal sealed class UpdateUserRoleHandler(
    UserManager<User> userManager,
    RoleManager<Role> roleManager,
    ILogger<UpdateUserRoleHandler> logger) : IUpdateUserRoleHandler
{
    /// <inheritdoc />
    public async Task<Result<UserResponse>> HandleAsync(
        string userId,
        string callerId,
        UpdateUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (string.Equals(userId, callerId, StringComparison.Ordinal))
        {
            return Error.Conflict("Users.CannotChangeOwnRole", "You cannot change your own role.");
        }

        if (!await roleManager.RoleExistsAsync(request.Role))
        {
            return UserErrors.RoleNotFound(request.Role);
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return UserErrors.NotFound(userId);
        }

        var currentRoles = await userManager.GetRolesAsync(user);

        // Remove then add, so the user ends with exactly one role. Adding without removing
        // would accumulate roles, and a user who is both Admin and User would hold the
        // union of both sets of claims — which is Admin, permanently.
        if (currentRoles.Count > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                return UserErrors.RoleUpdateFailed(removeResult.Errors);
            }
        }

        var addResult = await userManager.AddToRoleAsync(user, request.Role);
        if (!addResult.Succeeded)
        {
            return UserErrors.RoleUpdateFailed(addResult.Errors);
        }

        // Why this matters more than it looks: the user's permissions live in their access
        // token, which is still valid. They keep their old permissions until it expires —
        // up to AccessTokenMinutes. That is the cost of stateless tokens, and the reason
        // the access-token lifetime is short.
        logger.LogInformation(
            "User {UserId} role changed to {Role} by {CallerId}",
            userId,
            request.Role,
            callerId);

        return new UserResponse(user.Id, user.Email!, user.DisplayName, [request.Role]);
    }
}
