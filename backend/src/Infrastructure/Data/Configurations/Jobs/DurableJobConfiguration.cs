using Domain.Entities.Identity;
using Domain.Entities.Jobs;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Jobs;

/// <summary>
/// Representa a configuração da entidade DurableJob.
/// </summary>
internal sealed class DurableJobConfiguration : IEntityTypeConfiguration<DurableJob>
{
    public void Configure(EntityTypeBuilder<DurableJob> builder)
    {
        builder.ToTable("durable_jobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(j => j.TrainerId)
            .HasColumnName("trainer_id");

        builder.Property(j => j.JobType)
            .HasColumnName("job_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(j => j.JobVersion)
            .HasColumnName("job_version")
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(j => j.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(j => j.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired()
            .HasConversion(vo => vo.Value, value => JobStatus.FromString(value))
            .HasDefaultValue(JobStatus.Pending);

        builder.Property(j => j.ScheduledAt)
            .HasColumnName("scheduled_at")
            .IsRequired();

        builder.Property(j => j.Attempts)
            .HasColumnName("attempts")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(j => j.NextAttemptAt)
            .HasColumnName("next_attempt_at");

        builder.Property(j => j.LeaseOwnerId)
            .HasColumnName("lease_owner_id");

        builder.Property(j => j.LeaseExpiresAt)
            .HasColumnName("lease_expires_at");

        builder.Property(j => j.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(j => j.CorrelationId)
            .HasColumnName("correlation_id")
            .IsRequired();

        builder.Property(j => j.LastError)
            .HasColumnName("last_error");

        builder.Property(j => j.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(j => j.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.ToTable(tb =>
        {
            tb.HasCheckConstraint("status_check",
                "status IN ('pending', 'processing', 'completed', 'failed', 'dead_letter')");
            tb.HasCheckConstraint("durable_jobs_attempts_non_negative", "attempts >= 0");
        });

        builder.HasIndex(j => j.IdempotencyKey).HasDatabaseName("unique_idempotency_key").IsUnique();
        builder.HasIndex(j => j.ScheduledAt)
            .HasDatabaseName("idx_jobs_first_attempt")
            .HasFilter("status = 'pending' AND next_attempt_at IS NULL");

        builder.HasIndex(j => j.TrainerId).HasDatabaseName("idx_jobs_trainer");

        builder.HasIndex(j => j.LeaseExpiresAt)
            .HasDatabaseName("idx_jobs_lease")
            .HasFilter("status = 'processing'");

        builder.HasIndex(j => j.NextAttemptAt)
            .HasDatabaseName("idx_jobs_retry")
            .HasFilter("status = 'pending' AND next_attempt_at IS NOT NULL");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(j => j.TrainerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
