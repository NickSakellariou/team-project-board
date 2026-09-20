namespace Modules.Common.Domain;

/// <summary>
/// An entity whose creation and last-modification times are tracked automatically.
/// </summary>
/// <remarks>
/// Implementing this interface is all an entity has to do — <c>AuditableInterceptor</c>
/// sets both properties on the way to the database, so no handler ever writes
/// <c>entity.CreatedAtUtc = DateTime.UtcNow</c>. Forgetting that line in one of thirty
/// handlers is exactly the kind of bug this removes.
/// </remarks>
public interface IAuditableEntity
{
    /// <summary>Gets or sets the UTC time the row was inserted.</summary>
    DateTime CreatedAtUtc { get; set; }

    /// <summary>Gets or sets the UTC time the row was last updated, or null if never.</summary>
    DateTime? UpdatedAtUtc { get; set; }
}
