using Modules.Users.PublicApi.Contracts;

namespace Modules.Users.PublicApi;

/// <summary>
/// The only way another module may ask the Users module a question.
/// </summary>
/// <remarks>
/// <para>
/// This interface is the module's front door. Projects will need to check that a user
/// exists before adding them to a project, and to show member names on a board — but it
/// must not reach into <c>UsersDbContext</c> or join to the <c>users</c> schema to do it.
/// If it did, a change to the users table would break the Projects module, and the
/// boundary would exist only in the folder names.
/// </para>
/// <para>
/// Deliberately narrow. Every method added here is a promise to keep working, so the
/// interface should expose the questions other modules genuinely need answered and
/// nothing more. It returns <see cref="UserSummary"/> rather than the <c>User</c> entity
/// for the same reason: callers get a stable shape, not our storage model.
/// </para>
/// <para>
/// Today this is an in-process method call. Should Users ever become a separate service,
/// this interface is the seam that becomes an HTTP client, and no caller changes.
/// </para>
/// </remarks>
public interface IUserModuleApi
{
    /// <summary>
    /// Determines whether a user with this id exists.
    /// </summary>
    /// <param name="userId">The user id to check.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="true"/> if the user exists.</returns>
    Task<bool> UserExistsAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Looks up several users at once.
    /// </summary>
    /// <param name="userIds">The ids to look up.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Summaries of the users that exist; unknown ids are simply absent.</returns>
    /// <remarks>
    /// Takes a collection rather than one id because the caller's real question is "who
    /// are the twelve members of this board?". A single-id method would be called in a
    /// loop, and twelve round trips would replace one.
    /// </remarks>
    Task<IReadOnlyList<UserSummary>> GetUsersAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken);
}
