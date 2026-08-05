using Domain.Entities.Assessments;
using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Assessments;

/// <summary>
/// Representa a configuração da entidade CheckIn para o Entity Framework Core.
/// </summary>
internal sealed class CheckInConfiguration : IEntityTypeConfiguration<CheckIn>
{
    public void Configure(EntityTypeBuilder<CheckIn> builder)
    {
        builder.ToTable("checkins");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(c => c.OwnerTrainerId)
            .HasColumnName("owner_trainer_id")
            .IsRequired();

        builder.Property(c => c.ClientId)
            .HasColumnName("client_id")
            .IsRequired();

        builder.Property(c => c.CheckInDate)
            .HasColumnName("check_in_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(c => c.TargetDate)
            .HasColumnName("target_date")
            .HasColumnType("date");

        builder.Property(c => c.WeightKg)
            .HasColumnName("weight_kg")
            .HasPrecision(10, 2);

        builder.Property(c => c.BodyFatPercentage)
            .HasColumnName("body_fat_percentage")
            .HasPrecision(10, 2);

        builder.Property(c => c.Notes)
            .HasColumnName("notes");

        builder.ComplexProperty(c => c.BodyMeasurements, bm =>
        {
            bm.ToJson("body_measurements");
            bm.Property(b => b.WaistCm).HasJsonPropertyName("waist_cm").HasPrecision(10, 2);
            bm.Property(b => b.HipCm).HasJsonPropertyName("hip_cm").HasPrecision(10, 2);
            bm.Property(b => b.ChestCm).HasJsonPropertyName("chest_cm").HasPrecision(10, 2);
            bm.Property(b => b.RightArmCm).HasJsonPropertyName("right_arm_cm").HasPrecision(10, 2);
            bm.Property(b => b.LeftArmCm).HasJsonPropertyName("left_arm_cm").HasPrecision(10, 2);
            bm.Property(b => b.RightThighCm).HasJsonPropertyName("right_thigh_cm").HasPrecision(10, 2);
            bm.Property(b => b.LeftThighCm).HasJsonPropertyName("left_thigh_cm").HasPrecision(10, 2);
            bm.Property(b => b.RightCalfCm).HasJsonPropertyName("right_calf_cm").HasPrecision(10, 2);
            bm.Property(b => b.LeftCalfCm).HasJsonPropertyName("left_calf_cm").HasPrecision(10, 2);
        });

        builder.ComplexProperty(c => c.Feedback, fb =>
        {
            fb.ToJson("feedback");
            fb.Property(f => f.Appetite).HasJsonPropertyName("appetite");
            fb.Property(f => f.Digestion).HasJsonPropertyName("digestion");
            fb.Property(f => f.TrainingLoad).HasJsonPropertyName("training_load");
            fb.Property(f => f.RecoverySleep).HasJsonPropertyName("recovery_sleep");
            fb.Property(f => f.EnergyLevels).HasJsonPropertyName("energy_levels");
            fb.Property(f => f.BodyResponse).HasJsonPropertyName("body_response");
        });

        builder.Property(c => c.TrainingAdherenceScore)
            .HasColumnName("training_adherence_score");

        builder.Property(c => c.NutritionAdherenceScore)
            .HasColumnName("nutrition_adherence_score");

        builder.Property(c => c.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("checkin_date_order", "target_date IS NULL OR target_date >= check_in_date");
            t.HasCheckConstraint("checkin_weight_positive", "weight_kg IS NULL OR weight_kg > 0");
            t.HasCheckConstraint("checkin_body_fat_range",
                "body_fat_percentage IS NULL OR body_fat_percentage BETWEEN 0 AND 100");
            t.HasCheckConstraint("checkin_training_adherence_range",
                "training_adherence_score IS NULL OR training_adherence_score BETWEEN 0 AND 100");
            t.HasCheckConstraint("checkin_nutrition_adherence_range",
                "nutrition_adherence_score IS NULL OR nutrition_adherence_score BETWEEN 0 AND 100");
        });

        builder.HasIndex(c => c.OwnerTrainerId).HasDatabaseName("idx_checkins_trainer");
        builder.HasIndex(c => c.ClientId).HasDatabaseName("idx_checkins_client");
        builder.HasIndex(c => c.CheckInDate).HasDatabaseName("idx_checkins_date");
        builder.HasIndex(c => new { c.ClientId, c.CheckInDate })
            .HasDatabaseName("uq_checkins_client_date_active")
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.OwnerTrainerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(c => new { c.OwnerTrainerId, c.ClientId })
            .HasPrincipalKey(cl => new { cl.OwnerTrainerId, cl.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_checkins_client_tenant");
    }
}
