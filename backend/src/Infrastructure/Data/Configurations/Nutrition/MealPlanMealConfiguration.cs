using Domain.Entities.Nutrition;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Nutrition;

/// <summary>
/// Representa a configuração da entidade MealPlanMeal para o Entity Framework Core.
/// </summary>
internal sealed class MealPlanMealConfiguration : IEntityTypeConfiguration<MealPlanMeal>
{
    public void Configure(EntityTypeBuilder<MealPlanMeal> builder)
    {
        builder.ToTable("meal_plan_meals");
        builder.HasKey(mpm => mpm.Id);
        builder.Property(mpm => mpm.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(mpm => mpm.MealPlanId)
            .HasColumnName("meal_plan_id")
            .IsRequired();

        builder.Property(mpm => mpm.MealType)
            .HasColumnName("meal_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(mpm => mpm.OrderNumber)
            .HasColumnName("order_number")
            .IsRequired();

        builder.Property(mpm => mpm.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(mpm => mpm.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("meal_type_not_blank", "length(trim(meal_type)) > 0");
            t.HasCheckConstraint("meal_order_positive", "order_number > 0");
        });

        builder.HasIndex(mpm => new { mpm.MealPlanId, mpm.OrderNumber })
            .HasDatabaseName("unique_meal_order")
            .IsUnique();

        builder.HasIndex(mpm => mpm.MealPlanId)
            .HasDatabaseName("idx_meals_plan");

        builder.HasOne<MealPlan>()
            .WithMany(mp => mp.Meals)
            .HasForeignKey(mpm => mpm.MealPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
