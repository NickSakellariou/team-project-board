using Microsoft.Extensions.DependencyInjection;

namespace Modules.Common.Infrastructure.Database;

/// <summary>
/// Runs every registered module's migrations.
/// </summary>
public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Resolves all <see cref="IModuleDatabaseMigrator"/> implementations and runs each.
    /// </summary>
    /// <param name="scope">A DI scope to resolve migrators and DbContexts from.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when every module's schema is up to date.</returns>
    /// <remarks>
    /// Called at startup in Development only. In production, applying migrations from the
    /// running application is a poor idea: several instances would race each other, and a
    /// failed migration takes the app down with it. That belongs in a deployment step.
    /// </remarks>
    public static async Task MigrateModuleDatabasesAsync(
        this IServiceScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var migrators = scope.ServiceProvider.GetRequiredService<IEnumerable<IModuleDatabaseMigrator>>();

        foreach (var migrator in migrators)
        {
            await migrator.MigrateAsync(scope, cancellationToken);
        }
    }
}
