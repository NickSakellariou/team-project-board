using Modules.Common.Domain.Handlers;
using Modules.Common.Domain.Results;
using Modules.Users.Domain.Authentication;

namespace Modules.Users.Features.Users.LoginUser;

/// <summary>Handles <see cref="LoginUserRequest"/>.</summary>
internal interface ILoginUserHandler : IHandler
{
    /// <summary>Verifies the credentials and issues tokens.</summary>
    Task<Result<LoginUserResponse>> HandleAsync(LoginUserRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Signs a user in.
/// </summary>
/// <remarks>
/// Almost nothing happens here, and that is intentional. Credential checking, lockout and
/// token minting all live behind <see cref="IAuthenticationService"/> because they are
/// infrastructure. This handler's job is to be the use case — the thing the endpoint
/// names, the thing a test substitutes, and the place a rule like "block sign-in for
/// unconfirmed emails" would be added.
/// </remarks>
internal sealed class LoginUserHandler(IAuthenticationService authenticationService) : ILoginUserHandler
{
    /// <inheritdoc />
    public async Task<Result<LoginUserResponse>> HandleAsync(
        LoginUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.LoginAsync(request.Email, request.Password, cancellationToken);

        if (result.IsError)
        {
            return Result<LoginUserResponse>.FromErrors(result.Errors);
        }

        var tokens = result.Value!;

        return new LoginUserResponse(tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresAtUtc);
    }
}
