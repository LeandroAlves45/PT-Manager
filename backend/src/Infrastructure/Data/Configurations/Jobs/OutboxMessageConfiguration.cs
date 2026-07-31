using Domain.Entities.Identity;
using Domain.Entities.Jobs;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Jobs;

/// <summary>
/// Representa a configuração da entidade OuboxMessage.
/// </summary>
internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(m => m.TrainerId)
            .HasColumnName("trainer_id");

        builder.Property(m => m.MessageType)
            .HasColumnName("message_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(m => m.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(m => m.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired()
            .HasConversion(vo => vo.Value, value => JobStatus.FromString(value))
            .HasDefaultValue(JobStatus.Pending);

        builder.Property(m => m.CorrelationId)
            .HasColumnName("correlation_id")
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(m => m.CompletedAt)
            .HasColumnName("completed_at")
            .IsRequired();

        builder.Property(m => m.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(m => m.Attempts)
            .HasColumnName("attempts")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.NextAttemptAt)
            .HasColumnName("next_attempt_at");

        builder.Property(m => m.LeaseOwnerId)
            .HasColumnName("lease_owner_id");

        builder.Property(m => m.LeaseExpiresAt)
            .HasColumnName("lease_expires_at");

        builder.Property(m => m.LastError)
            .HasColumnName("last_error");

        builder.Property(m => m.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.ToTable(tb =>
        {
            tb.HasCheckConstraint("status_check",
                "status IN ('pending', 'processing', 'completed', 'failed', 'dead_letter')");
            tb.HasCheckConstraint("outbox_attempts_non_negative", "attempts >= 0");
        });

        builder.HasIndex(m => m.IdempotencyKey).HasDatabaseName("unique_outbox_idempotency_key").IsUnique();

        builder.HasIndex(m => m.CreatedAt)
            .HasDatabaseName("idx_outbox_first_attempt")
            .HasFilter("status = 'pending' AND next_attempt_at IS NULL");

        builder.HasIndex(m => m.NextAttemptAt)
            .HasDatabaseName("idx_outbox_retry")
            .HasFilter("status = 'pending' AND next_attempt_at IS NOT NULL");

        builder.HasIndex(m => m.LeaseExpiresAt)
            .HasDatabaseName("idx_outbox_lease")
            .HasFilter("status = 'processing'");

        builder.HasIndex(m => m.TrainerId).HasDatabaseName("idx_outbox_trainer");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(m => m.TrainerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
