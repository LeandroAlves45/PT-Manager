using Domain.Entities.Nutrition;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Nutrition;

/// <summary>
/// Representa a configuração da entidade MealPlanMealItem para o Entity Framework Core.
/// </summary>
internal sealed class MealPlanMealItemConfiguration : IEntityTypeConfiguration<MealPlanMealItem>
{
    public void Configure(EntityTypeBuilder<MealPlanMealItem> builder)
    {
        builder.ToTable("meal_plan_meal_items");
        builder.HasKey(mpmi => mpmi.Id);
        builder.Property(mpmi => mpmi.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(mpmi => mpmi.MealPlanMealId)
            .HasColumnName("meal_plan_meal_id")
            .IsRequired();

        builder.Property(mpmi => mpmi.FoodId)
            .HasColumnName("food_id")
            .IsRequired();

        builder.Property(mpmi => mpmi.QuantityInGrams)
            .HasColumnName("quantity_grams")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(mpmi => mpmi.OrderNumber)
            .HasColumnName("order_number")
            .IsRequired();

        builder.Property(mpmi => mpmi.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(mpmi => mpmi.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("positive_quantity", "quantity_grams > 0");
            t.HasCheckConstraint("meal_item_order_positive", "order_number > 0");
        });

        builder.HasIndex(mpmi => mpmi.MealPlanMealId)
            .HasDatabaseName("idx_items_meal");

        builder.HasIndex(mpmi => mpmi.FoodId)
            .HasDatabaseName("idx_meal_plan_meal_items_food");

        builder.HasOne<MealPlanMeal>()
            .WithMany(mpm => mpm.Items)
            .HasForeignKey(mpmi => mpmi.MealPlanMealId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Food>()
            .WithMany()
            .HasForeignKey(mpmi => mpmi.FoodId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_meal_plan_meal_items_food");
    }
}
