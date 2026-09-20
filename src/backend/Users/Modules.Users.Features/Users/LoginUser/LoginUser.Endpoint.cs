using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Modules.Common.API.Abstractions;
using Modules.Common.API.Extensions;
using Modules.Users.Features.Users.Shared.Routes;

namespace Modules.Users.Features.Users.LoginUser;

/// <summary>The body of a login request.</summary>
/// <param name="Email">The registered email address.</param>
/// <param name="Password">The plaintext password.</param>
public sealed record LoginUserRequest(string Email, string Password);

/// <summary>The token pair returned on a successful login.</summary>
/// <param name="AccessToken">Send as <c>Authorization: Bearer &lt;token&gt;</c>.</param>
/// <param name="RefreshToken">Exchange for a new pair once the access token expires.</param>
/// <param name="ExpiresAtUtc">When the access token stops working.</param>
public sealed record LoginUserResponse(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);

/// <summary>
/// <c>POST /api/users/login</c> — exchanges credentials for a token pair.
/// </summary>
public sealed class LoginUserEndpoint : IApiEndpoint
{
    /// <inheritdoc />
    public void MapEndpoint(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(RouteConsts.Login, HandleAsync)
            .AllowAnonymous()
            .WithTags(RouteConsts.Tag)
            .WithSummary("Sign in and receive an access token and refresh token.")
            .Produces<LoginUserResponse>()
            .ProducesValidationProblem();
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] LoginUserRequest request,
        IValidator<LoginUserRequest> validator,
        ILoginUserHandler handler,
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
