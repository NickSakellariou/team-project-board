using FluentValidation;

namespace Modules.Users.Features.Users.UpdateUser;

/// <summary>
/// Validates <see cref="UpdateUserRequest"/>.
/// </summary>
public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    /// <summary>Initialises the rules.</summary>
    public UpdateUserRequestValidator()
    {
        RuleFor(request => request.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .MaximumLength(128).WithMessage("Display name must be 128 characters or fewer.");
    }
}
