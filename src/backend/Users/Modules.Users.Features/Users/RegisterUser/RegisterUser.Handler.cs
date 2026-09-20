using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Modules.Common.Domain.Handlers;
using Modules.Common.Domain.Results;
using Modules.Users.Domain.Errors;
using Modules.Users.Domain.Logging;
using Modules.Users.Domain.Users;
using Modules.Users.Features.Users.Shared;

namespace Modules.Users.Features.Users.RegisterUser;

/// <summary>Handles <see cref="RegisterUserRequest"/>.</summary>
internal interface IRegisterUserHandler : IHandler
{
    /// <summary>Creates the account.</summary>
    Task<Result<UserResponse>> HandleAsync(RegisterUserRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Creates a user account and grants it the default <see cref="SystemRoles.User"/> role.
/// </summary>
/// <remarks>
/// Business rules: an email may be registered once, the password must meet the policy set
/// in <c>AddUsersIdentity</c>, and every new account is an ordinary user — the Admin role
/// is only ever granted by an existing admin, never self-assigned at registration.
/// </remarks>
internal sealed class RegisterUserHandler(
    UserManager<User> userManager,
    ILogger<RegisterUserHandler> logger) : IRegisterUserHandler
{
    /// <inheritdoc />
    public async Task<Result<UserResponse>> HandleAsync(
        RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = request.Email,
            // Identity requires a UserName and treats it as the login identifier. We log
            // in by email, so the two are the same value; leaving UserName unset would
            // fail Identity's own validation.
            UserName = request.Email,
            DisplayName = request.DisplayName
        };

        // CreateAsync does more than an INSERT: it normalizes the email and username,
        // hashes the password with PBKDF2, generates the security and concurrency stamps,
        // and runs Identity's own validators (unique email, password policy). This is the
        // work we would otherwise be writing — and getting subtly wrong.
        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            logger.RegistrationRejected(
                request.Email,
                string.Join(", ", createResult.Errors.Select(error => error.Code)));

            return UserErrors.RegistrationFailed(createResult.Errors);
        }

        var roleResult = await userManager.AddToRoleAsync(user, SystemRoles.User);
        if (!roleResult.Succeeded)
        {
            // The account exists but has no role, which would leave it in a half-created
            // state. Removing it keeps registration all-or-nothing.
            //
            // A real transaction would be better. It is not straightforward here because
            // UserManager owns its own SaveChanges calls, so the compensating delete is
            // the pragmatic choice — and this path only fires if role seeding failed,
            // which means the application is misconfigured anyway.
            await userManager.DeleteAsync(user);

            logger.DefaultRoleAssignmentFailed(user.Id);

            return UserErrors.RegistrationFailed(roleResult.Errors);
        }

        logger.UserRegistered(user.Id);

        return new UserResponse(user.Id, user.Email!, user.DisplayName, [SystemRoles.User]);
    }
}
