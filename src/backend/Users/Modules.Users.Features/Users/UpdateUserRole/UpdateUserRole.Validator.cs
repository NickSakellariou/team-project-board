using FluentValidation;
using Modules.Users.Domain.Users;

namespace Modules.Users.Features.Users.UpdateUserRole;

/// <summary>
/// Validates <see cref="UpdateUserRoleRequest"/>.
/// </summary>
public sealed class UpdateUserRoleRequestValidator : AbstractValidator<UpdateUserRoleRequest>
{
    /// <summary>Initialises the rules.</summary>
    public UpdateUserRoleRequestValidator()
    {
        RuleFor(request => request.Role)
            .NotEmpty().WithMessage("Role is required.")
            // A structural check against the known set, so an obvious typo gets a clear
            // 400 listing the valid values. The handler still asks RoleManager whether the
            // role exists, because the database is the authority — this rule only makes
            // the common mistake produce a better message.
            .Must(SystemRoles.All.Contains)
            .WithMessage($"Role must be one of: {string.Join(", ", SystemRoles.All)}.");
    }
}
