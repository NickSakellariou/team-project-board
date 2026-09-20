using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Modules.Common.Domain.Handlers;
using Modules.Common.Domain.Results;
using Modules.Users.Domain.Errors;
using Modules.Users.Domain.Logging;
using Modules.Users.Domain.Users;

namespace Modules.Users.Features.Users.DeleteUser;

/// <summary>Handles user deletion.</summary>
internal interface IDeleteUserHandler : IHandler
{
    /// <summary>Deletes the user, unless they are the caller.</summary>
    Task<Result<Success>> HandleAsync(string userId, string callerId, CancellationToken cancellationToken);
}

/// <summary>
/// Deletes a user account.
/// </summary>
/// <remarks>
/// Business rule: an admin cannot delete their own account. Not paternalism — it is the
/// cheapest guard against an organisation locking itself out by removing its last admin.
/// </remarks>
internal sealed class DeleteUserHandler(
    UserManager<User> userManager,
    ILogger<DeleteUserHandler> logger) : IDeleteUserHandler
{
    /// <inheritdoc />
    public async Task<Result<Success>> HandleAsync(
        string userId,
        string callerId,
        CancellationToken cancellationToken)
    {
        if (string.Equals(userId, callerId, StringComparison.Ordinal))
        {
            return UserErrors.CannotDeleteSelf();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return UserErrors.NotFound(userId);
        }

        var deleteResult = await userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
        {
            return UserErrors.DeleteFailed(deleteResult.Errors);
        }

        // The user's refresh tokens go with them: the foreign key in
        // RefreshTokenConfiguration cascades, so no token survives to be redeemed.
        logger.UserDeleted(userId, callerId);

        return Result.Success;
    }
}
