using Domain.Entities.Assessments;
using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Assessments;

/// <summary>
/// Configuração da entidade InitialAssessment
/// </summary>
internal sealed class InitialAssessmentsConfiguration : IEntityTypeConfiguration<InitialAssessment>
{
    public void Configure(EntityTypeBuilder<InitialAssessment> builder)
    {
        builder.ToTable("initial_assessments");
        builder.HasKey(ia => ia.Id);
        builder.Property(ia => ia.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(ia => ia.OwnerTrainerId)
            .HasColumnName("owner_trainer_id")
            .IsRequired();

        builder.Property(ia => ia.ClientId)
            .HasColumnName("client_id")
            .IsRequired();

        builder.Property(ia => ia.Age)
            .HasColumnName("age")
            .IsRequired();

        builder.Property(ia => ia.Gender)
            .HasColumnName("gender")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(ia => ia.WeightKg)
            .HasColumnName("weight_kg")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(ia => ia.HeightCm)
            .HasColumnName("height_cm")
            .IsRequired();

        builder.Property(ia => ia.FitnessLevel)
            .HasColumnName("fitness_level")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(ia => ia.Goals)
            .HasColumnName("goals")
            .IsRequired();

        builder.Property(ia => ia.BodyFatPercentage)
            .HasColumnName("body_fat_percentage")
            .HasPrecision(10, 2);

        builder.Property(ia => ia.MedicalConditions)
            .HasColumnName("medical_conditions");

        builder.Property(ia => ia.Profession)
            .HasColumnName("profession")
            .HasMaxLength(255);

        builder.ComplexProperty(ia => ia.BodyMeasurements, bm =>
        {
            bm.ToJson("body_measurements").HasColumnType("jsonb");
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

        builder.ComplexProperty(ia => ia.NutritionIntake, ni =>
        {
            ni.ToJson("nutrition_intake").HasColumnType("jsonb");
            ni.Property(n => n.FoodPreferences).HasJsonPropertyName("food_preferences");
            ni.Property(n => n.DislikedFoods).HasJsonPropertyName("disliked_foods");
            ni.Property(n => n.FoodIntolerances).HasJsonPropertyName("food_intolerances");
            ni.Property(n => n.FoodAllergies).HasJsonPropertyName("food_allergies");
            ni.Property(n => n.DietaryRestrictions).HasJsonPropertyName("dietary_restrictions");
            ni.Property(n => n.DailyRoutine).HasJsonPropertyName("daily_routine");
            ni.Property(n => n.SleepQuality).HasJsonPropertyName("sleep_quality");
            ni.Property(n => n.Mood).HasJsonPropertyName("mood");
            ni.Property(n => n.StressLevel).HasJsonPropertyName("stress_level");
            ni.Property(n => n.AvgWaterLitersPerDay).HasJsonPropertyName("avg_water_liters_per_day").HasPrecision(10, 2);
            ni.Property(n => n.HungriestTimeOfDay).HasJsonPropertyName("hungriest_time_of_day");
            ni.Property(n => n.UsesSupplements).HasJsonPropertyName("uses_supplements");
            ni.Property(n => n.CurrentSupplements).HasJsonPropertyName("current_supplements");
            ni.Property(n => n.OtherNotes).HasJsonPropertyName("other_notes");
        });

        builder.Property(ia => ia.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(ia => ia.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(ia => ia.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("assessment_age_positive", "age > 0");
            t.HasCheckConstraint("assessment_weight_positive", "weight_kg > 0");
            t.HasCheckConstraint("assessment_height_positive", "height_cm > 0");
            t.HasCheckConstraint("assessment_body_fat_range",
                "body_fat_percentage IS NULL OR body_fat_percentage BETWEEN 0 AND 100");
        });

        builder.HasIndex(ia => ia.OwnerTrainerId).HasDatabaseName("idx_assessments_trainer");
        builder.HasIndex(ia => ia.ClientId).HasDatabaseName("idx_assessments_client");
        builder.HasIndex(ia => ia.ClientId).HasDatabaseName("uq_initial_assessments_client").IsUnique();
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(ia => ia.OwnerTrainerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(ia => new { ia.OwnerTrainerId, ia.ClientId })
            .HasPrincipalKey(c => new { c.OwnerTrainerId, c.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_initial_assessments_client_tenant");
    }
}
