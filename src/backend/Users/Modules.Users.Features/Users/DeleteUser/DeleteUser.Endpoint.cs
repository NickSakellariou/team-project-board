using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Modules.Common.API.Abstractions;
using Modules.Common.API.Extensions;
using Modules.Common.Domain.Errors;
using Modules.Users.Domain.Policies;
using Modules.Users.Features.Users.Shared;
using Modules.Users.Features.Users.Shared.Routes;

namespace Modules.Users.Features.Users.DeleteUser;

/// <summary>
/// <c>DELETE /api/users/{userId}</c> — removes a user. Requires <c>users:delete</c>.
/// </summary>
public sealed class DeleteUserEndpoint : IApiEndpoint
{
    /// <inheritdoc />
    public void MapEndpoint(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete(RouteConsts.Delete, HandleAsync)
            .RequireAuthorization(UserPolicyConsts.Delete)
            .WithTags(RouteConsts.Tag)
            .WithSummary("Delete a user.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> HandleAsync(
        string userId,
        ClaimsPrincipal principal,
        IDeleteUserHandler handler,
        CancellationToken cancellationToken)
    {
        var callerId = principal.GetUserId();
        if (string.IsNullOrEmpty(callerId))
        {
            return RequestErrors.NotAuthenticated().ToProblemResult();
        }

        var result = await handler.HandleAsync(userId, callerId, cancellationToken);

        // 204 rather than 200 with a body: there is nothing meaningful to return about a
        // resource that no longer exists.
        return result.IsError
            ? result.Errors.ToProblem()
            : Results.NoContent();
    }
}
