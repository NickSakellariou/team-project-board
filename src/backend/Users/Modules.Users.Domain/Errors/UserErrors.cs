using Microsoft.AspNetCore.Identity;
using Modules.Common.Domain.Results;

namespace Modules.Users.Domain.Errors;

/// <summary>
/// Every failure the Users module can return.
/// </summary>
/// <remarks>
/// Collecting them in one file means the module's complete failure surface is readable at
/// a glance, error codes stay consistent (<c>Users.Something</c>), and the wording of a
/// message is changed in one place rather than hunted through handlers.
/// </remarks>
public static class UserErrors
{
    private const string Prefix = "Users";

    /// <summary>The user id does not exist.</summary>
    public static Error NotFound(string userId) =>
        Error.NotFound($"{Prefix}.{nameof(NotFound)}", $"User '{userId}' was not found.");

    /// <summary>No account uses that email address.</summary>
    public static Error NotFoundByEmail(string email) =>
        Error.NotFound($"{Prefix}.{nameof(NotFoundByEmail)}", $"No user found with email '{email}'.");

    /// <summary>The request is not authenticated, or the token carries no user id.</summary>
    public static Error NotAuthenticated() =>
        Error.Unauthorized($"{Prefix}.{nameof(NotAuthenticated)}", "The request is not authenticated.");

    /// <summary>
    /// The email or password is wrong.
    /// </summary>
    /// <remarks>
    /// Why one error for both cases: replying "no such user" to an unknown email and
    /// "wrong password" to a known one turns the login form into a tool for discovering
    /// which addresses have accounts. The distinction is in the log, not the response.
    /// </remarks>
    public static Error InvalidCredentials() =>
        Error.Validation($"{Prefix}.{nameof(InvalidCredentials)}", "Invalid email or password.");

    /// <summary>The account is locked after too many failed sign-in attempts.</summary>
    public static Error LockedOut() =>
        Error.Forbidden($"{Prefix}.{nameof(LockedOut)}", "This account is temporarily locked. Try again later.");

    /// <summary>The access token or refresh token was missing, malformed, expired or already used.</summary>
    public static Error InvalidToken() =>
        Error.Validation($"{Prefix}.{nameof(InvalidToken)}", "The token is invalid or has expired.");

    /// <summary>The named role does not exist.</summary>
    public static Error RoleNotFound(string roleName) =>
        Error.NotFound($"{Prefix}.{nameof(RoleNotFound)}", $"Role '{roleName}' was not found.");

    /// <summary>Registration failed, e.g. the email is taken or the password is too weak.</summary>
    public static Error RegistrationFailed(IEnumerable<IdentityError> identityErrors) =>
        Error.Conflict($"{Prefix}.{nameof(RegistrationFailed)}", Describe(identityErrors));

    /// <summary>Updating the user failed.</summary>
    public static Error UpdateFailed(IEnumerable<IdentityError> identityErrors) =>
        Error.Failure($"{Prefix}.{nameof(UpdateFailed)}", Describe(identityErrors));

    /// <summary>Deleting the user failed.</summary>
    public static Error DeleteFailed(IEnumerable<IdentityError> identityErrors) =>
        Error.Failure($"{Prefix}.{nameof(DeleteFailed)}", Describe(identityErrors));

    /// <summary>Changing the user's role failed.</summary>
    public static Error RoleUpdateFailed(IEnumerable<IdentityError> identityErrors) =>
        Error.Failure($"{Prefix}.{nameof(RoleUpdateFailed)}", Describe(identityErrors));

    // Identity reports failures as a collection of IdentityError, each with its own code
    // and description ("PasswordTooShort", "DuplicateEmail"). Flattening them into one
    // description keeps our Error a single value while preserving what Identity said.
    private static string Describe(IEnumerable<IdentityError> identityErrors) =>
        string.Join(" ", identityErrors.Select(error => error.Description));
}
