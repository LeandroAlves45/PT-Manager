using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Identity;

/// <summary>Configura os convites de treinador isento enviados pelo superuser.</summary>
internal sealed class TrainerInvitationConfiguration : IEntityTypeConfiguration<TrainerInvitation>
{
    public void Configure(EntityTypeBuilder<TrainerInvitation> builder)
    {
        builder.ToTable("trainer_invitations");
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(invitation => invitation.Email)
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();
        builder.Property(invitation => invitation.NormalizedEmail)
            .HasColumnName("normalized_email")
            .HasMaxLength(255)
            .IsRequired();
        builder.Property(invitation => invitation.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(255)
            .IsRequired();
        builder.Property(invitation => invitation.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();
        builder.Property(invitation => invitation.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(invitation => invitation.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();
        builder.Property(invitation => invitation.UsedAt).HasColumnName("used_at");
        builder.Property(invitation => invitation.RevokedAt).HasColumnName("revoked_at");

        builder.Ignore(invitation => invitation.IsUsed);
        builder.Ignore(invitation => invitation.IsRevoked);
        builder.Ignore(invitation => invitation.IsPending);

        builder.HasIndex(invitation => invitation.TokenHash)
            .HasDatabaseName("uq_trainer_invitations_hash")
            .IsUnique();
        // Um só convite pendente por email: enviar outro anula o anterior na mesma transação.
        builder.HasIndex(invitation => invitation.NormalizedEmail)
            .HasDatabaseName("uq_trainer_invitations_pending_email")
            .HasFilter("used_at IS NULL AND revoked_at IS NULL")
            .IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(invitation => invitation.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_trainer_invitations_creator");
    }
}
