using Domain.Entities.Clients;
using Domain.Entities.Training;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Training;

/// <summary>
/// Configura a conclusão de treino pelo cliente: uma por cliente, dia de plano e data local
/// com associação client-tenant reforçada na DB.
/// </summary>
internal sealed class WorkoutCompletionConfiguration : IEntityTypeConfiguration<WorkoutCompletion>
{
    public void Configure(EntityTypeBuilder<WorkoutCompletion> builder)
    {
        builder.ToTable("workout_completions");
        builder.HasKey(completion => completion.Id);
        builder.Property(completion => completion.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(completion => completion.OwnerTrainerId)
            .HasColumnName("owner_trainer_id")
            .IsRequired();

        builder.Property(completion => completion.ClientId)
            .HasColumnName("client_id")
            .IsRequired();

        builder.Property(completion => completion.TrainingPlanId)
            .HasColumnName("training_plan_id")
            .IsRequired();

        builder.Property(completion => completion.TrainingPlanDayId)
            .HasColumnName("training_plan_day_id")
            .IsRequired();

        builder.Property(completion => completion.LocalDate)
            .HasColumnName("local_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(completion => completion.Notes)
            .HasColumnName("notes")
            .HasMaxLength(WorkoutCompletion.NotesMaxLength);

        builder.Property(completion => completion.CompletedAt)
            .HasColumnName("completed_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(completion => new
        {
            completion.ClientId,
            completion.TrainingPlanDayId,
            completion.LocalDate
        })
            .HasDatabaseName("uq_workout_completions_client_day_date")
            .IsUnique();

        builder.HasIndex(completion => new
        {
            completion.OwnerTrainerId,
            completion.ClientId,
            completion.LocalDate
        })
            .HasDatabaseName("idx_workout_completions_tenant_client_date");

        builder.HasIndex(completion => completion.TrainingPlanDayId)
            .HasDatabaseName("idx_workout_completions_day");

        builder.HasIndex(completion => completion.TrainingPlanId)
            .HasDatabaseName("idx_workout_completions_plan");

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(completion => new { completion.OwnerTrainerId, completion.ClientId })
            .HasPrincipalKey(client => new { client.OwnerTrainerId, client.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_workout_completions_client_tenant");

        builder.HasOne<TrainingPlan>()
            .WithMany()
            .HasForeignKey(completion => completion.TrainingPlanId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_workout_completions_training_plan");

        builder.HasOne<TrainingPlanDay>()
            .WithMany()
            .HasForeignKey(completion => completion.TrainingPlanDayId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_workout_completions_training_plan_day");
    }
}
