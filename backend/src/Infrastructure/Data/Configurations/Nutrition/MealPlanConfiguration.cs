using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Domain.Entities.Nutrition;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Nutrition;

/// <summary>
/// Representa a configuração da entidade MealPlan para o Entity Framework Core.
/// </summary>
internal sealed class MealPlanConfiguration : IEntityTypeConfiguration<MealPlan>
{
    public void Configure(EntityTypeBuilder<MealPlan> builder)
    {
        builder.ToTable("meal_plans");
        builder.HasKey(mp => mp.Id);
        builder.Property(mp => mp.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.ComplexProperty(mp => mp.Targets, t =>
        {
            t.Property(tp => tp.ProteinGrams)
                .HasColumnName("protein_target_g")
                .HasPrecision(10, 2)
                .IsRequired();
            t.Property(tp => tp.CarbsGrams)
                .HasColumnName("carbs_target_g")
                .HasPrecision(10, 2)
                .IsRequired();
            t.Property(tp => tp.FatsGrams)
                .HasColumnName("fats_target_g")
                .HasPrecision(10, 2)
                .IsRequired();
            t.Ignore(tp => tp.Kcal);
        });

        builder.ComplexProperty(mp => mp.CalculationSnapshot, snapshot =>
        {
            snapshot.ToJson("calculation_snapshot");
            snapshot.Property(value => value.SchemaVersion).HasJsonPropertyName("schema_version");
            snapshot.Property(value => value.CalculationOrigin).HasJsonPropertyName("calculation_origin");
            snapshot.Property(value => value.CalculatedAt).HasJsonPropertyName("calculated_at");
            snapshot.Property(value => value.EnergyFormula).HasJsonPropertyName("energy_formula");
            snapshot.Property(value => value.WeightKgUsed).HasJsonPropertyName("weight_kg_used").HasPrecision(10, 2);
            snapshot.Property(value => value.HeightCmUsed).HasJsonPropertyName("height_cm_used").HasPrecision(10, 2);
            snapshot.Property(value => value.AgeUsed).HasJsonPropertyName("age_used");
            snapshot.Property(value => value.SexUsed).HasJsonPropertyName("sex_used");
            snapshot.Property(value => value.BodyFatPercentageUsed).HasJsonPropertyName("body_fat_percentage_used").HasPrecision(10, 2);
            snapshot.Property(value => value.ActivityLevel).HasJsonPropertyName("activity_level");
            snapshot.Property(value => value.ActivityFactor).HasJsonPropertyName("activity_factor").HasPrecision(10, 3);
            snapshot.Property(value => value.GoalType).HasJsonPropertyName("goal_type");
            snapshot.Property(value => value.GoalAdjustmentKcal).HasJsonPropertyName("goal_adjustment_kcal").HasPrecision(10, 2);
            snapshot.Property(value => value.RestingEnergyExpenditureKcal).HasJsonPropertyName("resting_energy_expenditure_kcal").HasPrecision(10, 2);
            snapshot.Property(value => value.TotalDailyEnergyExpenditureKcal).HasJsonPropertyName("total_daily_energy_expenditure_kcal").HasPrecision(10, 2);
            snapshot.Property(value => value.TargetKcal).HasJsonPropertyName("target_kcal").HasPrecision(10, 2);
            snapshot.Property(value => value.MacroDistributionMode).HasJsonPropertyName("macro_distribution_mode");
            snapshot.Property(value => value.ProteinPercentageInput).HasJsonPropertyName("protein_percentage_input").HasPrecision(5, 2);
            snapshot.Property(value => value.CarbsPercentageInput).HasJsonPropertyName("carbs_percentage_input").HasPrecision(5, 2);
            snapshot.Property(value => value.FatsPercentageInput).HasJsonPropertyName("fats_percentage_input").HasPrecision(5, 2);
            snapshot.Property(value => value.ProteinGramsPerKgInput).HasJsonPropertyName("protein_grams_per_kg_input").HasPrecision(10, 2);
            snapshot.Property(value => value.FatsGramsPerKgInput).HasJsonPropertyName("fats_grams_per_kg_input").HasPrecision(10, 2);
            snapshot.Property(value => value.ProteinTargetGrams).HasJsonPropertyName("protein_target_grams").HasPrecision(10, 2);
            snapshot.Property(value => value.CarbsTargetGrams).HasJsonPropertyName("carbs_target_grams").HasPrecision(10, 2);
            snapshot.Property(value => value.FatsTargetGrams).HasJsonPropertyName("fats_target_grams").HasPrecision(10, 2);
            snapshot.Property(value => value.ProteinEnergyPercentage).HasJsonPropertyName("protein_energy_percentage").HasPrecision(5, 2);
            snapshot.Property(value => value.CarbsEnergyPercentage).HasJsonPropertyName("carbs_energy_percentage").HasPrecision(5, 2);
            snapshot.Property(value => value.FatsEnergyPercentage).HasJsonPropertyName("fats_energy_percentage").HasPrecision(5, 2);
            snapshot.Property(value => value.CalculatedMacroKcal).HasJsonPropertyName("calculated_macro_kcal").HasPrecision(10, 2);
            snapshot.Property(value => value.KcalDifference).HasJsonPropertyName("kcal_difference").HasPrecision(10, 2);
        });

        builder.Property(mp => mp.KcalTarget)
            .HasColumnName("kcal_target")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(mp => mp.OwnerTrainerId)
            .HasColumnName("owner_trainer_id")
            .IsRequired();

        builder.Property(mp => mp.ClientId)
            .HasColumnName("client_id")
            .IsRequired();

        builder.Property(mp => mp.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(mp => mp.Description)
            .HasColumnName("description");

        builder.Property(mp => mp.StartsDate)
            .HasColumnName("starts_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(mp => mp.EndsDate)
            .HasColumnName("ends_date")
            .HasColumnType("date");

        builder.Property(mp => mp.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(mp => mp.IsArchived)
            .HasColumnName("is_archived")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(mp => mp.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(mp => mp.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(mp => mp.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_meal_plans_date_order", "starts_date <= ends_date");
            t.HasCheckConstraint(
                "ck_meal_plans_targets",
                "kcal_target > 0 " +
                "AND protein_target_g >= 0 " +
                "AND carbs_target_g >= 0 " +
                "AND fats_target_g >= 0 " +
                "AND abs((protein_target_g * 4 + carbs_target_g * 4 + " +
                "fats_target_g * 9) - kcal_target) <= 100");
        });

        builder.HasIndex(mp => mp.OwnerTrainerId)
            .HasDatabaseName("idx_meal_plans_trainer");

        builder.HasIndex(mp => mp.ClientId)
            .HasDatabaseName("idx_meal_plans_client");

        builder.HasIndex(mp => new { mp.OwnerTrainerId, mp.IsActive })
            .HasDatabaseName("idx_meal_plans_trainer_active");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(mp => mp.OwnerTrainerId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_meal_plans_owner_trainer");

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(mp => new { mp.OwnerTrainerId, mp.ClientId })
            .HasPrincipalKey(c => new { c.OwnerTrainerId, c.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_meal_plans_client_tenant");
    }
}
