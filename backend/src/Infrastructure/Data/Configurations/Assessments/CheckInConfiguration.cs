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
            bm.ToJson("body_measurements").HasColumnType("jsonb");
            bm.Property(b => b.WaistCm).HasColumnName("waist_cm").HasPrecision(10, 2);
            bm.Property(b => b.HipCm).HasColumnName("hip_cm").HasPrecision(10, 2);
            bm.Property(b => b.ChestCm).HasColumnName("chest_cm").HasPrecision(10, 2);
            bm.Property(b => b.RightArmCm).HasColumnName("right_arm_cm").HasPrecision(10, 2);
            bm.Property(b => b.LeftArmCm).HasColumnName("left_arm_cm").HasPrecision(10, 2);
            bm.Property(b => b.RightThighCm).HasColumnName("right_thigh_cm").HasPrecision(10, 2);
            bm.Property(b => b.LeftThighCm).HasColumnName("left_thigh_cm").HasPrecision(10, 2);
            bm.Property(b => b.RightCalfCm).HasColumnName("right_calf_cm").HasPrecision(10, 2);
            bm.Property(b => b.LeftCalfCm).HasColumnName("left_calf_cm").HasPrecision(10, 2);
        });

        builder.ComplexProperty(c => c.Feedback, fb =>
        {
            fb.ToJson("feedback").HasColumnType("jsonb");
            fb.Property(f => f.Appetite).HasColumnName("appetite");
            fb.Property(f => f.Digestion).HasColumnName("digestion");
            fb.Property(f => f.TrainingLoad).HasColumnName("training_load");
            fb.Property(f => f.RecoverySleep).HasColumnName("recovery_sleep");
            fb.Property(f => f.EnergyLevels).HasColumnName("energy_levels");
            fb.Property(f => f.BodyResponse).HasColumnName("body_response");
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
