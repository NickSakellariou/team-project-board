using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Modules.Common.API.Abstractions;
using Modules.Common.API.Extensions;
using Modules.Users.Features.Users.Shared.Routes;

namespace Modules.Users.Features.Users.RefreshToken;

/// <summary>The body of a refresh request.</summary>
/// <param name="AccessToken">The expired access token. Its signature is still verified.</param>
/// <param name="RefreshToken">The refresh token issued alongside it.</param>
public sealed record RefreshTokenRequest(string AccessToken, string RefreshToken);

/// <summary>The new token pair.</summary>
/// <param name="AccessToken">The replacement access token.</param>
/// <param name="RefreshToken">A new refresh token; the old one is now spent.</param>
/// <param name="ExpiresAtUtc">When the new access token stops working.</param>
public sealed record RefreshTokenResponse(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);

/// <summary>
/// <c>POST /api/users/refresh</c> — trades an expired token pair for a fresh one.
/// </summary>
/// <remarks>
/// Anonymous, and necessarily so: the caller's access token has expired, so the JWT
/// middleware would reject the request before it ever reached this endpoint. The
/// credentials being checked are the two tokens in the body, not the Authorization header.
/// </remarks>
public sealed class RefreshTokenEndpoint : IApiEndpoint
{
    /// <inheritdoc />
    public void MapEndpoint(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(RouteConsts.Refresh, HandleAsync)
            .AllowAnonymous()
            .WithTags(RouteConsts.Tag)
            .WithSummary("Exchange an expired access token and its refresh token for a new pair.")
            .Produces<RefreshTokenResponse>()
            .ProducesValidationProblem();
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] RefreshTokenRequest request,
        IValidator<RefreshTokenRequest> validator,
        IRefreshTokenHandler handler,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var result = await handler.HandleAsync(request, cancellationToken);

        return result.IsError
            ? result.Errors.ToProblem()
            : Results.Ok(result.Value);
    }
}
