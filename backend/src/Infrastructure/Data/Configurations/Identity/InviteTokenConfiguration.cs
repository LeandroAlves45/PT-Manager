using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Identity;

/// <summary>
/// Representa o mapeamento da entidade InviteToken para o Entity Framework Core.
/// </summary>
internal sealed class InviteTokenConfiguration : IEntityTypeConfiguration<InviteToken>
{
    public void Configure(EntityTypeBuilder<InviteToken> builder)
    {
        builder.ToTable("invite_tokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(token => token.TrainerId)
            .HasColumnName("trainer_id")
            .IsRequired();

        builder.Property(token => token.Email)
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(token => token.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(token => token.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(token => token.IsUsed)
            .HasColumnName("is_used")
            .HasDefaultValue(false);

        builder.Property(token => token.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => token.TrainerId).HasDatabaseName("idx_invites_trainer");
        builder.HasIndex(token => token.Email).HasDatabaseName("idx_invites_email");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.TrainerId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}
