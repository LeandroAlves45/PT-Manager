using Domain.Entities.Training;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Training;

/// <summary>
/// Representa a configuração da entidade TrainingPlanDayExercise para o Entity Framework Core.
/// </summary>
internal sealed class TrainingPlanDayExerciseConfiguration : IEntityTypeConfiguration<TrainingPlanDayExercise>
{
    public void Configure(EntityTypeBuilder<TrainingPlanDayExercise> builder)
    {
        builder.ToTable("training_plan_day_exercises");
        builder.HasKey(tpde => tpde.Id);
        builder.Property(tpde => tpde.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(tpde => tpde.TrainingPlanDayId)
            .HasColumnName("training_plan_day_id")
            .IsRequired();

        builder.Property(tpde => tpde.ExerciseId)
            .HasColumnName("exercise_id")
            .IsRequired();

        builder.Property(tpde => tpde.OrderNumber)
            .HasColumnName("order_number")
            .IsRequired();

        builder.Property(tpde => tpde.ExerciseGroupId)
            .HasColumnName("exercise_group_id");

        builder.Property(tpde => tpde.GroupPosition)
            .HasColumnName("group_position");

        builder.Property(tpde => tpde.Notes)
            .HasColumnName("notes");

        builder.Property(tpde => tpde.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(tpde => tpde.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(tpde => tpde.TrainingPlanDayId)
            .HasDatabaseName("idx_day_exercises_day");

        builder.HasIndex(tpde => tpde.ExerciseId)
            .HasDatabaseName("idx_training_plan_day_exercises_exercise");

        builder.HasIndex(tpde => new { tpde.TrainingPlanDayId, tpde.OrderNumber })
            .HasFilter("exercise_group_id IS NULL")
            .HasDatabaseName("uq_day_exercise_isolated_order")
            .IsUnique();
        builder.HasIndex(tpde => new
        {
            tpde.TrainingPlanDayId,
            tpde.ExerciseGroupId,
            tpde.GroupPosition
        })
            .HasFilter("exercise_group_id IS NOT NULL")
            .HasDatabaseName("uq_day_exercise_group_position")
            .IsUnique();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("day_exercise_order_positive", "order_number > 0");
            t.HasCheckConstraint(
                "day_exercise_group_consistency",
                "(exercise_group_id IS NULL AND group_position IS NULL) OR " +
                "(exercise_group_id IS NOT NULL AND group_position > 0)"
            );
        });

        builder.HasOne<TrainingPlanDay>()
            .WithMany(day => day.Exercises)
            .HasForeignKey(tpde => tpde.TrainingPlanDayId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Exercise>()
            .WithMany()
            .HasForeignKey(tpde => tpde.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_training_plan_day_exercises_exercise");
    }
}
