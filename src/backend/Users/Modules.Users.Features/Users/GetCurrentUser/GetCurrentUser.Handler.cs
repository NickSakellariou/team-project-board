using Microsoft.AspNetCore.Identity;
using Modules.Common.Domain.Handlers;
using Modules.Common.Domain.Results;
using Modules.Users.Domain.Errors;
using Modules.Users.Domain.Users;
using Modules.Users.Features.Users.Shared;

namespace Modules.Users.Features.Users.GetCurrentUser;

/// <summary>Handles the "who am I" query.</summary>
internal interface IGetCurrentUserHandler : IHandler
{
    /// <summary>Loads the user behind the supplied id.</summary>
    Task<Result<UserResponse>> HandleAsync(string userId, CancellationToken cancellationToken);
}

/// <summary>
/// Returns the signed-in user's profile.
/// </summary>
/// <remarks>
/// The token already carries the id, email and roles, so this could be answered without a
/// database read at all. It reads anyway: the token is a snapshot from up to
/// <c>AccessTokenMinutes</c> ago, and a user who just changed their display name should
/// see the new one rather than be told to sign in again.
/// </remarks>
internal sealed class GetCurrentUserHandler(UserManager<User> userManager) : IGetCurrentUserHandler
{
    /// <inheritdoc />
    public async Task<Result<UserResponse>> HandleAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            // Reachable with a still-valid token belonging to a since-deleted account.
            return UserErrors.NotFound(userId);
        }

        var roles = await userManager.GetRolesAsync(user);

        return new UserResponse(user.Id, user.Email!, user.DisplayName, [.. roles]);
    }
}
