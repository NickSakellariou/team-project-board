using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Users.Domain.Tokens;
using Modules.Users.Domain.Users;

namespace Modules.Users.Infrastructure.Database.Mapping;

/// <summary>
/// Maps <see cref="RefreshToken"/> to its table.
/// </summary>
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("refresh_token");

        // The token value is the key: every lookup is "find the row for this token", so a
        // surrogate id would add a column and a second index for nothing.
        builder.HasKey(token => token.Token);

        builder.Property(token => token.Token).HasMaxLength(128);
        builder.Property(token => token.JwtId).HasMaxLength(128).IsRequired();
        builder.Property(token => token.UserId).HasMaxLength(450).IsRequired();

        // A foreign key to the user, with cascade delete: deleting a user must not leave
        // tokens behind that could still be redeemed. This FK is allowed because both
        // tables are inside this module's schema — the rule the architecture forbids is a
        // foreign key that crosses into *another* module's schema.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Supports "invalidate every token this user holds", which a logout-everywhere or
        // a detected replay needs to do.
        builder.HasIndex(token => token.UserId).HasDatabaseName("ix_refresh_token_user_id");
    }
}
