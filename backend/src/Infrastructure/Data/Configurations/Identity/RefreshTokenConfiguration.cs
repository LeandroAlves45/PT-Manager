using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Identity;

/// <summary>
/// Representa o mapeamento da entidade RefreshToken para o Entity Framework Core.
/// </summary>
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(token => token.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(token => token.FamilyId)
            .HasColumnName("family_id")
            .IsRequired();

        builder.Property(token => token.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(token => token.RotatedFromId)
            .HasColumnName("rotated_from_id");

        builder.Property(token => token.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(token => token.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(token => token.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(token => token.TokenHash)
            .IsUnique();

        builder.HasIndex(token => token.UserId).HasDatabaseName("idx_refresh_tokens_user");
        builder.HasIndex(token => token.FamilyId).HasDatabaseName("idx_refresh_tokens_family");
        builder.HasIndex(token => token.ExpiresAt).HasDatabaseName("idx_refresh_tokens_expires");

        // Auto-referência: a cadeia de rotação.
        builder.HasOne<RefreshToken>()
            .WithMany()
            .HasForeignKey(token => token.RotatedFromId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
