using Domain.Entities.Identity;
using Domain.Entities.Nutrition;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Nutrition;

/// <summary>Configura alimentos e a coluna kcal derivada.</summary>
internal sealed class FoodConfiguration : IEntityTypeConfiguration<Food>
{
    public void Configure(EntityTypeBuilder<Food> builder)
    {
        builder.ToTable("foods", table => table.HasCheckConstraint(
            "ck_foods_nutrients_per_100g",
            "protein BETWEEN 0 AND 100 " +
            "AND carbs BETWEEN 0 AND 100 " +
            "AND fats BETWEEN 0 AND 100 " +
            "AND protein + carbs + fats <= 100 " +
            "AND (fiber IS NULL OR fiber >= 0)"));
        builder.HasKey(food => food.Id);
        builder.Property(food => food.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(food => food.OwnerTrainerId).HasColumnName("owner_trainer_id");
        builder.Property(food => food.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(food => food.Description).HasColumnName("description");
        builder.Property(food => food.Protein).HasColumnName("protein").HasPrecision(10, 2).IsRequired();
        builder.Property(food => food.Carbs).HasColumnName("carbs").HasPrecision(10, 2).IsRequired();
        builder.Property(food => food.Fats).HasColumnName("fats").HasPrecision(10, 2).IsRequired();
        builder.Property(food => food.Kcal)
            .HasColumnName("kcal")
            .HasPrecision(10, 2)
            .HasComputedColumnSql("protein * 4 + carbs * 4 + fats * 9", stored: true);
        builder.Property(food => food.Fiber).HasColumnName("fiber").HasPrecision(10, 2);
        builder.Property(food => food.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();
        builder.Property(food => food.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(food => food.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();
        builder.Property(food => food.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(food => new { food.OwnerTrainerId, food.Name })
            .HasDatabaseName("idx_foods_owner_name");
        builder.HasIndex(food => new { food.Name, food.Description })
            .HasMethod("GIN")
            .IsTsVectorExpressionIndex("portuguese")
            .HasDatabaseName("idx_foods_search");

        builder.HasOne<User>().WithMany().HasForeignKey(food => food.OwnerTrainerId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_foods_owner_trainer");
    }
}
