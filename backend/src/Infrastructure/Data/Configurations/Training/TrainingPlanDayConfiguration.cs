using Domain.Entities.Training;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Training;

/// <summary>
/// Representa a configuração da entidade TrainingPlanDay para o Entity Framework Core.
/// </summary>
internal sealed class TrainingPlanDayConfiguration : IEntityTypeConfiguration<TrainingPlanDay>
{
    public void Configure(EntityTypeBuilder<TrainingPlanDay> builder)
    {
        builder.ToTable("training_plan_days");
        builder.HasKey(tpd => tpd.Id);
        builder.Property(tpd => tpd.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(tpd => tpd.DayOfWeek)
            .HasColumnName("day_of_week")
            .IsRequired();

        builder.Property(tpd => tpd.TrainingPlanId)
            .HasColumnName("training_plan_id")
            .IsRequired();

        builder.Property(tpd => tpd.WeekNumber)
            .HasColumnName("week_number")
            .IsRequired();

        builder.Property(tpd => tpd.Notes)
            .HasColumnName("notes");

        builder.Property(tpd => tpd.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(tpd => tpd.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("day_range", "day_of_week >= 0 AND day_of_week <= 6");
            t.HasCheckConstraint("week_range", "week_number >= 1 AND week_number <= 52");
        });

        builder.HasIndex(tpd => tpd.TrainingPlanId)
            .HasDatabaseName("idx_days_plan");

        builder.HasOne<TrainingPlan>()
            .WithMany()
            .HasForeignKey(tpd => tpd.TrainingPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
