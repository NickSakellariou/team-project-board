using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Modules.Common.API.Abstractions;
using Modules.Common.API.Extensions;
using Modules.Common.Domain.Errors;
using Modules.Users.Features.Users.Shared;
using Modules.Users.Features.Users.Shared.Routes;

namespace Modules.Users.Features.Users.GetCurrentUser;

/// <summary>
/// <c>GET /api/users/me</c> — returns the signed-in user.
/// </summary>
/// <remarks>
/// The one endpoint with no route parameter and no request body: its input is the identity
/// on the token. That is deliberate — a client asking "who am I?" must not be able to
/// answer "someone else" by changing a URL.
/// </remarks>
public sealed class GetCurrentUserEndpoint : IApiEndpoint
{
    /// <inheritdoc />
    public void MapEndpoint(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(RouteConsts.Me, HandleAsync)
            // RequireAuthorization with no policy means "any valid token will do". Every
            // signed-in user may read their own record, whatever their role.
            .RequireAuthorization()
            .WithTags(RouteConsts.Tag)
            .WithSummary("Get the currently authenticated user.")
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal principal,
        IGetCurrentUserHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            // Should be unreachable: RequireAuthorization has already rejected anonymous
            // callers. It fires only if a token validated but carried no user id, which
            // would mean a bug in token creation — worth failing loudly rather than
            // dereferencing null.
            return RequestErrors.NotAuthenticated().ToProblemResult();
        }

        var result = await handler.HandleAsync(userId, cancellationToken);

        return result.IsError
            ? result.Errors.ToProblem()
            : Results.Ok(result.Value);
    }
}
