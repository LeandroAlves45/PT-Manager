using Domain.Entities.Training;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Training;

/// <summary>
/// Representa a configuração da entidade ExerciseSet para o Entity Framework Core.
/// </summary>
internal sealed class ExerciseSetConfiguration : IEntityTypeConfiguration<ExerciseSet>
{
    public void Configure(EntityTypeBuilder<ExerciseSet> builder)
    {
        builder.ToTable("exercise_sets");
        builder.HasKey(es => es.Id);
        builder.Property(es => es.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(es => es.TrainingPlanDayExerciseId)
            .HasColumnName("training_plan_day_exercise_id")
            .IsRequired();

        builder.Property(es => es.SetNumber)
            .HasColumnName("set_number")
            .IsRequired();

        builder.Property(es => es.PlannedReps)
            .HasColumnName("planned_reps");

        builder.Property(es => es.PlannedWeightKg)
            .HasColumnName("planned_weight_kg")
            .HasPrecision(10, 2);

        builder.Property(es => es.RestSecondsMin)
            .HasColumnName("rest_seconds_min");

        builder.Property(es => es.RestSecondsMax)
            .HasColumnName("rest_seconds_max");

        builder.Property(es => es.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(es => es.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("set_num_check", "set_number >= 1 AND set_number <= 15");
            t.HasCheckConstraint("reps_check", "planned_reps IS NULL OR planned_reps > 0");
            t.HasCheckConstraint("planned_weight_check", "planned_weight_kg IS NULL OR planned_weight_kg >= 0");
            t.HasCheckConstraint("rest_min_check", "rest_seconds_min IS NULL OR rest_seconds_min >= 0");
            t.HasCheckConstraint("rest_max_check", "rest_seconds_max IS NULL OR rest_seconds_max >= 0");
            t.HasCheckConstraint("rest_range_check",
                "rest_seconds_min IS NULL OR rest_seconds_max IS NULL OR rest_seconds_min <= rest_seconds_max");
        });

        builder.HasIndex(es => es.TrainingPlanDayExerciseId)
            .HasDatabaseName("idx_sets_exercise");
        builder.HasIndex(es => new { es.TrainingPlanDayExerciseId, es.SetNumber })
            .HasDatabaseName("uq_exercise_set_number")
            .IsUnique();

        builder.HasOne<TrainingPlanDayExercise>()
            .WithMany(exercise => exercise.Sets)
            .HasForeignKey(es => es.TrainingPlanDayExerciseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
