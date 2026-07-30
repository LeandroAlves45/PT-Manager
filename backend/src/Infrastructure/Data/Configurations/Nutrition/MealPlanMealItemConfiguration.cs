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
            .ValueGeneratedOnAdd();

        builder.Property(mpmi => mpmi.MealPlanMealId)
            .HasColumnName("meal_plan_meal_id")
            .IsRequired();

        builder.Property(mpmi => mpmi.FoodId)
            .HasColumnName("food_id")
            .IsRequired();

        builder.Property(mpmi => mpmi.QuantityInGrams)
            .HasColumnName("quantity_in_grams")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(mpmi => mpmi.OrderNumber)
            .HasColumnName("order_number")
            .IsRequired();

        builder.ToTable(t => t.HasCheckConstraint("positive_quantity", "quantity_in_grams > 0"));

        builder.HasIndex(mpmi => mpmi.MealPlanMealId)
            .HasDatabaseName("idx_items_meal");

        builder.HasOne<MealPlanMeal>()
            .WithMany(mpm => mpm.Items)
            .HasForeignKey(mpmi => mpmi.MealPlanMealId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Food>()
            .WithMany()
            .HasForeignKey(mpmi => mpmi.FoodId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
