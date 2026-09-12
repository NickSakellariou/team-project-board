using FluentValidation;

namespace Modules.Users.Features.Users.RegisterUser;

/// <summary>
/// Validates <see cref="RegisterUserRequest"/>.
/// </summary>
/// <remarks>
/// <para>
/// Structural checks only — anything answerable without touching the database. "Is this
/// shaped like an email?" belongs here; "is this email already taken?" does not, because
/// that is a business rule whose answer changes over time and which the database must
/// enforce anyway to survive two simultaneous registrations.
/// </para>
/// <para>
/// The password rules here deliberately duplicate part of Identity's own policy. The
/// duplication buys a better error message: Identity would reject the request too, but as
/// a 409 mentioning "PasswordTooShort" after the request has already reached the handler.
/// </para>
/// </remarks>
public sealed class RegisterUserRequestValidator : AbstractValidator<RegisterUserRequest>
{
    /// <summary>Initialises the rules.</summary>
    public RegisterUserRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .MaximumLength(256).WithMessage("Email must be 256 characters or fewer.");

        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            // Why an upper bound: PBKDF2 hashes the whole input, so a multi-megabyte
            // password is a cheap way to make the server burn CPU. 128 is far beyond any
            // real password.
            .MaximumLength(128).WithMessage("Password must be 128 characters or fewer.");

        RuleFor(request => request.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .MaximumLength(128).WithMessage("Display name must be 128 characters or fewer.");
    }
}
