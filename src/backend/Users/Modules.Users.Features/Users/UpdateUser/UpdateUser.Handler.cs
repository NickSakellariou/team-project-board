using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Modules.Common.Domain.Handlers;
using Modules.Common.Domain.Results;
using Modules.Users.Domain.Errors;
using Modules.Users.Domain.Users;
using Modules.Users.Features.Users.Shared;

namespace Modules.Users.Features.Users.UpdateUser;

/// <summary>Handles <see cref="UpdateUserRequest"/>.</summary>
internal interface IUpdateUserHandler : IHandler
{
    /// <summary>Applies the update.</summary>
    Task<Result<UserResponse>> HandleAsync(string userId, UpdateUserRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Updates a user's display name.
/// </summary>
internal sealed class UpdateUserHandler(
    UserManager<User> userManager,
    ILogger<UpdateUserHandler> logger) : IUpdateUserHandler
{
    /// <inheritdoc />
    public async Task<Result<UserResponse>> HandleAsync(
        string userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return UserErrors.NotFound(userId);
        }

        user.DisplayName = request.DisplayName;

        // UpdateAsync rather than a direct SaveChanges: it also refreshes the concurrency
        // stamp, which is what makes two simultaneous edits to this user detectable
        // instead of silently last-write-wins.
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return UserErrors.UpdateFailed(updateResult.Errors);
        }

        logger.LogInformation("Updated profile for user {UserId}", user.Id);

        var roles = await userManager.GetRolesAsync(user);

        return new UserResponse(user.Id, user.Email!, user.DisplayName, [.. roles]);
    }
}
