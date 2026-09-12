using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Modules.Common.Domain;

namespace Modules.Common.Infrastructure.Database;

/// <summary>
/// Stamps <see cref="IAuditableEntity.CreatedAtUtc"/> and
/// <see cref="IAuditableEntity.UpdatedAtUtc"/> as changes are saved.
/// </summary>
/// <remarks>
/// <para>
/// An EF Core interceptor hooks into the <c>SaveChanges</c> pipeline. This one runs just
/// before EF generates SQL, walks the change tracker, and fills in the timestamps — so no
/// handler has to remember to.
/// </para>
/// <para>
/// It is registered as a <b>singleton</b> even though the <c>DbContext</c> it serves is
/// scoped. That is safe because the interceptor holds no state of its own: everything it
/// touches arrives as a method argument. Registering it scoped would allocate one per
/// request for no reason. See docs/concepts/dependency-injection.md.
/// </para>
/// </remarks>
public sealed class AuditableInterceptor : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is not null)
        {
            ApplyTimestamps(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is not null)
        {
            ApplyTimestamps(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private static void ApplyTimestamps(DbContext context)
    {
        // Why UtcNow and not Now: the server's local time zone is an accident of where the
        // container happens to run. Storing UTC keeps timestamps comparable across
        // environments; converting to the user's zone is the frontend's job.
        var timestamp = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = timestamp;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = timestamp;

                    // Why: without this, an UPDATE statement would include created_at_utc
                    // and overwrite the original creation time with whatever the loaded
                    // entity happens to hold.
                    entry.Property(nameof(IAuditableEntity.CreatedAtUtc)).IsModified = false;
                    break;

                default:
                    break;
            }
        }
    }
}
