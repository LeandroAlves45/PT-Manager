using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Identity;

/// <summary>Configura convites de uso único ligados a uma ficha de cliente.</summary>
internal sealed class InviteTokenConfiguration : IEntityTypeConfiguration<InviteToken>
{
    public void Configure(EntityTypeBuilder<InviteToken> builder)
    {
        builder.ToTable("invite_tokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(token => token.TrainerId).HasColumnName("trainer_id").IsRequired();
        builder.Property(token => token.ClientId).HasColumnName("client_id").IsRequired();
        builder.Property(token => token.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
        builder.Property(token => token.TokenHash).HasColumnName("token_hash").HasMaxLength(255).IsRequired();
        builder.Property(token => token.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(token => token.UsedAt).HasColumnName("used_at");
        builder.Property(token => token.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Ignore(token => token.IsUsed);

        builder.HasIndex(token => token.TokenHash)
            .HasDatabaseName("uq_invite_tokens_hash")
            .IsUnique();
        builder.HasIndex(token => new { token.TrainerId, token.ClientId, token.ExpiresAt })
            .HasDatabaseName("idx_invite_tokens_client_expiry");
        builder.HasIndex(token => new { token.TrainerId, token.Email })
            .HasDatabaseName("idx_invite_tokens_trainer_email");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.TrainerId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_invite_tokens_trainer");

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(token => new { token.TrainerId, token.ClientId })
            .HasPrincipalKey(client => new { client.OwnerTrainerId, client.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_invite_tokens_client_tenant");
    }
}
