using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Identity;

/// <summary>Mapeia a auditoria de append-only de transferências.</summary>
internal sealed class TenantTransferAuditConfiguration : IEntityTypeConfiguration<TenantTransferAudit>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TenantTransferAudit> builder)
    {
        builder.ToTable("tenant_transfer_audits");
        builder.HasKey(audit => audit.Id);

        builder.Property(audit => audit.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(audit => audit.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(audit => audit.SourceTrainerId)
            .HasColumnName("source_trainer_id")
            .IsRequired();

        builder.Property(audit => audit.TargetTrainerId)
            .HasColumnName("target_trainer_id")
            .IsRequired();

        builder.Property(audit => audit.TargetClientId)
            .HasColumnName("target_client_id")
            .IsRequired();

        builder.Property(audit => audit.OccurredAt)
            .HasColumnName("occurred_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(audit => new { audit.UserId, audit.OccurredAt })
            .HasDatabaseName("idx_tenant_transfer_audits_user_occurred");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(audit => audit.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_tenant_transfer_audits_user");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(audit => audit.SourceTrainerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_tenant_transfer_audits_source_trainer");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(audit => audit.TargetTrainerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_tenant_transfer_audits_target_trainer");

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(audit => new { audit.TargetTrainerId, audit.TargetClientId })
            .HasPrincipalKey(client => new { client.OwnerTrainerId, client.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_tenant_transfer_audits_target_client_tenant");
    }
}

