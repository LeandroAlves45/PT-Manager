using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Identity;

/// <summary>Configura o mapeamento da entidade ExternalAuthenticationChallenge para a base de dados.</summary>
internal sealed class ExternalAuthenticationChallengeConfiguration
    : IEntityTypeConfiguration<ExternalAuthenticationChallenge>
{
    public void Configure(EntityTypeBuilder<ExternalAuthenticationChallenge> builder)
    {
        builder.ToTable("external_authentication_challenges", table =>
        {
            table.HasCheckConstraint("ck_external_auth_challenges_purpose",
                "purpose IN ('sign_in', 'link')");
            table.HasCheckConstraint("ck_external_auth_challenges_actor",
                "(purpose = 'sign_in' AND user_id IS NULL) OR (purpose = 'link' AND user_id IS NOT NULL)");
            table.HasCheckConstraint("ck_external_auth_challenges_expiration",
                "expires_at > created_at");
        });

        builder.HasKey(challenge => challenge.Id);
        builder.Property(challenge => challenge.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(challenge => challenge.NonceHash)
            .HasColumnName("nonce_hash")
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();

        builder.Property(challenge => challenge.Purpose)
            .HasColumnName("purpose")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(challenge => challenge.UserId)
            .HasColumnName("user_id");

        builder.Property(challenge => challenge.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(challenge => challenge.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(challenge => challenge.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(challenge => challenge.NonceHash)
            .HasDatabaseName("uq_external_auth_challenges_nonce_hash")
            .IsUnique();

        builder.HasIndex(challenge => challenge.ExpiresAt)
            .HasDatabaseName("idx_external_auth_challenges_expires_at");
    }
}
