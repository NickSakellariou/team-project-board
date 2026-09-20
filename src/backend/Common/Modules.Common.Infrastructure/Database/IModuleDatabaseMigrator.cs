using Microsoft.Extensions.DependencyInjection;

namespace Modules.Common.Infrastructure.Database;

/// <summary>
/// Applies one module's pending EF Core migrations.
/// </summary>
/// <remarks>
/// Each module owns a separate <c>DbContext</c> and schema, so there is no single
/// "migrate the database" call. Every module registers its own implementation, and the
/// host resolves them all and runs each in turn — meaning the host does not need to know
/// which modules exist or name their contexts.
/// </remarks>
public interface IModuleDatabaseMigrator
{
    /// <summary>
    /// Applies any migrations this module has not yet run.
    /// </summary>
    /// <param name="scope">A DI scope from which to resolve the module's DbContext.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the module's schema is up to date.</returns>
    Task MigrateAsync(IServiceScope scope, CancellationToken cancellationToken = default);
}
