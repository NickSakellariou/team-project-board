using Microsoft.EntityFrameworkCore;
using Modules.Users.Infrastructure.Database;
using Modules.Users.PublicApi;
using Modules.Users.PublicApi.Contracts;

namespace Modules.Users.Features.InternalApi;

/// <summary>
/// Implements <see cref="IUserModuleApi"/> for in-process callers.
/// </summary>
/// <remarks>
/// Internal, so no other module can reference the class — only the interface in the
/// PublicApi project, which is what keeps the boundary enforceable rather than merely
/// documented.
/// </remarks>
internal sealed class UserModuleApi(UsersDbContext dbContext) : IUserModuleApi
{
    /// <inheritdoc />
    public Task<bool> UserExistsAsync(string userId, CancellationToken cancellationToken) =>
        dbContext.Users
            // AnyAsync issues SELECT EXISTS(...) — the database stops at the first match
            // and returns a boolean, instead of materializing a whole user row to discard.
            .AnyAsync(user => user.Id == userId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserSummary>> GetUsersAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Count == 0)
        {
            // Guard rather than let EF build `WHERE id IN ()`, which is a needless
            // round trip for a question we already know the answer to.
            return [];
        }

        return await dbContext.Users
            // AsNoTracking: this is a read whose results are never modified, so the change
            // tracker's snapshot of every row would be pure overhead.
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            // Projecting in the query means SELECT id, email, display_name rather than
            // every column including the password hash.
            .Select(user => new UserSummary(user.Id, user.Email!, user.DisplayName))
            .ToListAsync(cancellationToken);
    }
}
