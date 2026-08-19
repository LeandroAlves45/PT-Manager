using Domain.Entities.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Administration;

/// <summary>Configura o registo administrativo append-only.</summary>
internal sealed class AdministrativeAuditEntryConfiguration
    : IEntityTypeConfiguration<AdministrativeAuditEntry>
{
    public void Configure(EntityTypeBuilder<AdministrativeAuditEntry> builder)
    {
        builder.ToTable("administrative_audit_entries", table =>
        {
            table.HasCheckConstraint(
                "ck_administrative_audit_entries_state",
                "before_state IS NOT NULL OR after_state IS NOT NULL");
        });

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(entry => entry.ActorUserId)
            .HasColumnName("actor_user_id")
            .IsRequired();

        builder.Property(entry => entry.Action)
            .HasColumnName("action")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(entry => entry.ResourceType)
            .HasColumnName("resource_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(entry => entry.ResourceId)
            .HasColumnName("resource_id")
            .IsRequired();

        builder.Property(entry => entry.BeforeState)
            .HasColumnName("before_state")
            .HasColumnType("jsonb");

        builder.Property(entry => entry.AfterState)
            .HasColumnName("after_state")
            .HasColumnType("jsonb");

        builder.Property(entry => entry.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.HasIndex(entry => new { entry.ResourceType, entry.ResourceId, entry.OccurredAt })
            .HasDatabaseName("idx_administrative_audit_resource_time");

        builder.HasIndex(entry => new { entry.ActorUserId, entry.OccurredAt })
            .HasDatabaseName("idx_administrative_audit_actor_time");
    }
}
