using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Modules.Common.API.Abstractions;
using Modules.Common.API.Extensions;
using Modules.Users.Domain.Policies;
using Modules.Users.Features.Users.Shared;
using Modules.Users.Features.Users.Shared.Routes;

namespace Modules.Users.Features.Users.GetUserById;

/// <summary>
/// <c>GET /api/users/{userId}</c> — returns any user. Requires <c>users:read</c>.
/// </summary>
public sealed class GetUserByIdEndpoint : IApiEndpoint
{
    /// <inheritdoc />
    public void MapEndpoint(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(RouteConsts.GetById, HandleAsync)
            // Reading *another* user's record is an administrative act, so it needs the
            // claim rather than merely a valid token. Compare /me above, which any
            // authenticated caller may use.
            .RequireAuthorization(UserPolicyConsts.Read)
            .WithTags(RouteConsts.Tag)
            .WithSummary("Get a user by id.")
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // userId binds from the route template because the parameter name matches {userId}.
    private static async Task<IResult> HandleAsync(
        string userId,
        IGetUserByIdHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(userId, cancellationToken);

        return result.IsError
            ? result.Errors.ToProblem()
            : Results.Ok(result.Value);
    }
}
