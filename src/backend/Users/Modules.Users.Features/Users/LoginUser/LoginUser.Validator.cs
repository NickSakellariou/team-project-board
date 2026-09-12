using FluentValidation;

namespace Modules.Users.Features.Users.LoginUser;

/// <summary>
/// Validates <see cref="LoginUserRequest"/>.
/// </summary>
/// <remarks>
/// Presence checks only. Note the absence of the password length rule that registration
/// has: applying the current policy at login would reject users whose password predates a
/// policy change, and would leak the policy to anyone probing the endpoint.
/// </remarks>
public sealed class LoginUserRequestValidator : AbstractValidator<LoginUserRequest>
{
    /// <summary>Initialises the rules.</summary>
    public LoginUserRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("Email is required.");

        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
