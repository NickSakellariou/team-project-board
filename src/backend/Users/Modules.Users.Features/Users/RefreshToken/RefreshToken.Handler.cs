using Modules.Common.Domain.Handlers;
using Modules.Common.Domain.Results;
using Modules.Users.Domain.Authentication;

namespace Modules.Users.Features.Users.RefreshToken;

/// <summary>Handles <see cref="RefreshTokenRequest"/>.</summary>
internal interface IRefreshTokenHandler : IHandler
{
    /// <summary>Rotates the token pair.</summary>
    Task<Result<RefreshTokenResponse>> HandleAsync(RefreshTokenRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Issues a replacement token pair and spends the old refresh token.
/// </summary>
internal sealed class RefreshTokenHandler(IAuthenticationService authenticationService) : IRefreshTokenHandler
{
    /// <inheritdoc />
    public async Task<Result<RefreshTokenResponse>> HandleAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.RefreshAsync(
            request.AccessToken,
            request.RefreshToken,
            cancellationToken);

        if (result.IsError)
        {
            return Result<RefreshTokenResponse>.FromErrors(result.Errors);
        }

        var tokens = result.Value!;

        return new RefreshTokenResponse(tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresAtUtc);
    }
}
