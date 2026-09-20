using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Common.Infrastructure.Database;

namespace Modules.Users.Infrastructure.Database;

/// <summary>
/// Applies the Users module's migrations.
/// </summary>
internal sealed class UsersDatabaseMigrator : IModuleDatabaseMigrator
{
    /// <inheritdoc />
    public async Task MigrateAsync(IServiceScope scope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var dbContext = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
