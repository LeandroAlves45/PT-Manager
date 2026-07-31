using Domain.Entities.Clients;
using Domain.Entities.Training;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Training;

/// <summary>
/// Representa a configuração da entidade ClientExerciseSetLog para o Entity Framework Core.
/// </summary>
internal sealed class ClientExerciseSetLogConfiguration : IEntityTypeConfiguration<ClientExerciseSetLog>
{
    public void Configure(EntityTypeBuilder<ClientExerciseSetLog> builder)
    {
        builder.ToTable("client_exercise_set_logs");
        builder.HasKey(cel => cel.Id);
        builder.Property(cel => cel.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(cel => cel.ClientId)
            .HasColumnName("client_id")
            .IsRequired();

        builder.Property(cel => cel.TrainingPlanDayExerciseId)
            .HasColumnName("training_plan_day_exercise_id")
            .IsRequired();

        builder.Property(cel => cel.SetNumber)
            .HasColumnName("set_number")
            .IsRequired();

        builder.Property(cel => cel.WeightKg)
            .HasColumnName("weight_kg")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(cel => cel.RepsDone)
            .HasColumnName("reps_done")
            .IsRequired();

        builder.Property(cel => cel.Notes)
            .HasColumnName("notes")
            .HasMaxLength(500);

        builder.Property(cel => cel.LoggedAt)
            .HasColumnName("logged_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(cel => cel.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(cel => cel.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("set_num_check", "set_number >= 1 AND set_number <= 15");
            t.HasCheckConstraint("reps_check", "reps_done >= 0 AND reps_done <= 100");
            t.HasCheckConstraint("weight_check", "weight_kg >= 0");
        });

        builder.HasIndex(cel => new { cel.ClientId, cel.TrainingPlanDayExerciseId, cel.SetNumber })
            .HasDatabaseName("unique_set_log")
            .IsUnique();
        builder.HasIndex(cel => cel.ClientId)
            .HasDatabaseName("idx_logs_client");
        builder.HasIndex(cel => cel.TrainingPlanDayExerciseId)
            .HasDatabaseName("idx_logs_exercise");

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(cel => cel.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<TrainingPlanDayExercise>()
            .WithMany()
            .HasForeignKey(cel => cel.TrainingPlanDayExerciseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
