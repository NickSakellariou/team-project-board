using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Users.Domain.Users;

namespace Modules.Users.Infrastructure.Database.Mapping;

/// <summary>
/// Maps <see cref="User"/> to its table.
/// </summary>
/// <remarks>
/// Keeping mapping in a separate class rather than attributes on the entity is what lets
/// the Domain project stay free of persistence concerns — the entity does not know it is
/// stored, let alone how wide its columns are.
/// </remarks>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Property(user => user.DisplayName)
            .HasMaxLength(128)
            .IsRequired();

        // Why set a length at all: without it Npgsql maps a string to `text`, which is
        // unbounded. That works, but it means the database enforces nothing and a bug
        // (or a malicious client) can store a megabyte in a name field.
        builder.Property(user => user.Email).HasMaxLength(256);
        builder.Property(user => user.NormalizedEmail).HasMaxLength(256);
        builder.Property(user => user.UserName).HasMaxLength(256);
        builder.Property(user => user.NormalizedUserName).HasMaxLength(256);

        // Identity already indexes NormalizedEmail, but not uniquely — it allows duplicate
        // emails unless you opt in. We treat email as the login identifier, so a unique
        // index makes the database the final guard: even with two concurrent registrations
        // racing past the application's check, only one can commit.
        builder.HasIndex(user => user.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("ix_user_normalized_email");
    }
}
