using Domain.Entities.Clients;
using Domain.Entities.Nutrition;
using Domain.Entities.Supplements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Supplements;

/// <summary>
/// Tomas dos suplementos do plano alimentar: uma por prescrição e dia local. As referências à
/// prescrição ficam a null se o personal trainer a tirar do plano, para o histórico ficar.
/// </summary>
internal sealed class ClientMealPlanSupplementIntakeConfiguration
    : IEntityTypeConfiguration<ClientMealPlanSupplementIntake>
{
    public void Configure(EntityTypeBuilder<ClientMealPlanSupplementIntake> builder)
    {
        builder.ToTable("client_meal_plan_supplement_intakes");
        builder.HasKey(intake => intake.Id);
        builder.Property(intake => intake.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(intake => intake.OwnerTrainerId).HasColumnName("owner_trainer_id").IsRequired();
        builder.Property(intake => intake.ClientId).HasColumnName("client_id").IsRequired();
        builder.Property(intake => intake.MealPlanId).HasColumnName("meal_plan_id").IsRequired();
        builder.Property(intake => intake.MealPlanMealSupplementId).HasColumnName("meal_plan_meal_supplement_id");
        builder.Property(intake => intake.MealPlanSupplementId).HasColumnName("meal_plan_supplement_id");
        builder.Property(intake => intake.SupplementId).HasColumnName("supplement_id").IsRequired();

        builder.Property(intake => intake.LocalDate)
            .HasColumnName("local_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(intake => intake.TakenAt)
            .HasColumnName("taken_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_client_meal_plan_supplement_intakes_single_reference",
            "num_nonnulls(meal_plan_meal_supplement_id, meal_plan_supplement_id) <= 1"));

        builder.HasIndex(intake => new { intake.MealPlanMealSupplementId, intake.LocalDate })
            .HasDatabaseName("uq_client_meal_plan_supplement_intakes_meal_supplement_date")
            .IsUnique()
            .HasFilter("meal_plan_meal_supplement_id IS NOT NULL");

        builder.HasIndex(intake => new { intake.MealPlanSupplementId, intake.LocalDate })
            .HasDatabaseName("uq_client_meal_plan_supplement_intakes_plan_supplement_date")
            .IsUnique()
            .HasFilter("meal_plan_supplement_id IS NOT NULL");

        builder.HasIndex(intake => new { intake.OwnerTrainerId, intake.ClientId, intake.MealPlanId, intake.LocalDate })
            .HasDatabaseName("idx_client_meal_plan_supplement_intakes_client_plan_date");

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(intake => new { intake.OwnerTrainerId, intake.ClientId })
            .HasPrincipalKey(client => new { client.OwnerTrainerId, client.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_client_meal_plan_supplement_intakes_client_tenant");

        builder.HasOne<MealPlan>()
            .WithMany()
            .HasForeignKey(intake => intake.MealPlanId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_client_meal_plan_supplement_intakes_meal_plan");

        builder.HasOne<MealPlanMealSupplement>()
            .WithMany()
            .HasForeignKey(intake => intake.MealPlanMealSupplementId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_client_meal_plan_supplement_intakes_meal_supplement");

        builder.HasOne<MealPlanSupplement>()
            .WithMany()
            .HasForeignKey(intake => intake.MealPlanSupplementId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_client_meal_plan_supplement_intakes_plan_supplement");

        builder.HasOne<Supplement>()
            .WithMany()
            .HasForeignKey(intake => intake.SupplementId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_client_meal_plan_supplement_intakes_supplement");
    }
}
