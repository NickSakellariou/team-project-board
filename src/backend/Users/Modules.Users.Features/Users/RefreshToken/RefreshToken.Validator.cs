using FluentValidation;

namespace Modules.Users.Features.Users.RefreshToken;

/// <summary>
/// Validates <see cref="RefreshTokenRequest"/>.
/// </summary>
public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    /// <summary>Initialises the rules.</summary>
    public RefreshTokenRequestValidator()
    {
        RuleFor(request => request.AccessToken)
            .NotEmpty().WithMessage("Access token is required.");

        RuleFor(request => request.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}
