using Domain.Entities.Nutrition;
using Domain.Entities.Supplements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Nutrition;

/// <summary>Suplementos do plano alimentar fora de uma refeição.</summary>
internal sealed class MealPlanSupplementConfiguration : IEntityTypeConfiguration<MealPlanSupplement>
{
    public void Configure(EntityTypeBuilder<MealPlanSupplement> builder)
    {
        builder.ToTable("meal_plan_supplements");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(item => item.MealPlanId)
            .HasColumnName("meal_plan_id")
            .IsRequired();

        builder.Property(item => item.SupplementId)
            .HasColumnName("supplement_id")
            .IsRequired();

        builder.Property(item => item.Notes)
            .HasColumnName("notes")
            .HasMaxLength(500);

        builder.Property(item => item.Quantity)
            .HasColumnName("quantity")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(item => item.OrderNumber)
            .HasColumnName("order_number")
            .IsRequired();

        builder.Property(item => item.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(item => item.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_meal_plan_supplements_quantity_positive", "quantity > 0");
            t.HasCheckConstraint("ck_meal_plan_supplements_order_positive", "order_number > 0");
        });

        builder.HasIndex(item => new { item.MealPlanId, item.SupplementId })
            .HasDatabaseName("uq_meal_plan_supplements_plan_supplement")
            .IsUnique();

        builder.HasOne<MealPlan>()
            .WithMany(plan => plan.PlanSupplements)
            .HasForeignKey(item => item.MealPlanId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_meal_plan_supplements_meal_plan");

        builder.HasOne<Supplement>()
            .WithMany()
            .HasForeignKey(item => item.SupplementId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_meal_plan_supplements_supplement");
    }
}
