using Domain.Entities.Nutrition;
using Domain.Entities.Supplements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Nutrition;

/// <summary>
/// Representa a configuração da entidade MealPlanMealSupplement para o Entity Framework Core.
/// </summary>
internal sealed class MealPlanMealSupplementConfiguration : IEntityTypeConfiguration<MealPlanMealSupplement>
{
    public void Configure(EntityTypeBuilder<MealPlanMealSupplement> builder)
    {
        builder.ToTable("meal_plan_meal_supplements");
        builder.HasKey(mpms => mpms.Id);
        builder.Property(mpms => mpms.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(mpms => mpms.MealPlanMealId)
            .HasColumnName("meal_plan_meal_id")
            .IsRequired();

        builder.Property(mpms => mpms.SupplementId)
            .HasColumnName("supplement_id")
            .IsRequired();

        builder.Property(mpms => mpms.Notes)
            .HasColumnName("notes")
            .HasMaxLength(500);

        builder.Property(mpms => mpms.Quantity)
            .HasColumnName("quantity")
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(mpms => mpms.OrderNumber)
            .HasColumnName("order_number")
            .IsRequired();

        builder.Property(mpms => mpms.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(mpms => mpms.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("positive_supplement_quantity", "quantity > 0");
            t.HasCheckConstraint("meal_supplement_order_positive", "order_number > 0");
        });

        builder.HasIndex(mpms => new { mpms.MealPlanMealId, mpms.SupplementId })
            .HasDatabaseName("unique_supplement_per_meal")
            .IsUnique();

        builder.HasIndex(mpms => mpms.MealPlanMealId)
            .HasDatabaseName("idx_supp_meal");

        builder.HasOne<MealPlanMeal>()
            .WithMany(mpm => mpm.Supplements)
            .HasForeignKey(mpms => mpms.MealPlanMealId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Supplement>()
            .WithMany()
            .HasForeignKey(mpms => mpms.SupplementId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_meal_plan_meal_supplements_supplement");
    }
}

