using Microsoft.AspNetCore.Identity;
using Modules.Common.Domain.Handlers;
using Modules.Common.Domain.Results;
using Modules.Users.Domain.Errors;
using Modules.Users.Domain.Users;
using Modules.Users.Features.Users.Shared;

namespace Modules.Users.Features.Users.GetUserById;

/// <summary>Handles the "fetch a user by id" query.</summary>
internal interface IGetUserByIdHandler : IHandler
{
    /// <summary>Loads one user.</summary>
    Task<Result<UserResponse>> HandleAsync(string userId, CancellationToken cancellationToken);
}

/// <summary>
/// Returns one user's profile.
/// </summary>
internal sealed class GetUserByIdHandler(UserManager<User> userManager) : IGetUserByIdHandler
{
    /// <inheritdoc />
    public async Task<Result<UserResponse>> HandleAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return UserErrors.NotFound(userId);
        }

        var roles = await userManager.GetRolesAsync(user);

        return new UserResponse(user.Id, user.Email!, user.DisplayName, [.. roles]);
    }
}
