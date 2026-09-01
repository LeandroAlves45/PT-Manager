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
        builder.ToTable("foods", table =>
        {
            table.HasCheckConstraint(
                "ck_foods_nutrients_per_100g",
                "protein BETWEEN 0 AND 100 " +
                "AND carbs BETWEEN 0 AND 100 " +
                "AND fats BETWEEN 0 AND 100 " +
                "AND protein + carbs + fats <= 100 " +
                "AND (fiber IS NULL OR fiber >= 0)");

            table.HasCheckConstraint(
                "ck_foods_platform_enforcement",
                "(platform_enforcement_status = 'allowed' AND platform_enforcement_reason IS NULL " +
                "AND platform_enforced_at IS NULL) OR " +
                "(platform_enforcement_status = 'blocked' AND owner_trainer_id IS NOT NULL " +
                "AND platform_enforcement_reason IS NOT NULL " +
                "AND platform_enforcement_reason IN ('malicious_content', 'dangerous_information', " +
                "'deliberately_false_information', 'prohibited_content') AND platform_enforced_at IS NOT NULL)"
            );
        });

        builder.HasKey(food => food.Id);
        builder.Property(food => food.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(food => food.OwnerTrainerId).HasColumnName("owner_trainer_id");

        builder.Property(food => food.Name).HasColumnName("name").HasMaxLength(255).IsRequired();

        builder.Property(food => food.Description).HasColumnName("description");

        builder.Property(food => food.Protein)
            .HasColumnName("protein").HasPrecision(10, 2).IsRequired();

        builder.Property(food => food.Carbs)
            .HasColumnName("carbs").HasPrecision(10, 2).IsRequired();

        builder.Property(food => food.Fats)
            .HasColumnName("fats").HasPrecision(10, 2).IsRequired();

        builder.Property(food => food.Kcal)
            .HasColumnName("kcal")
            .HasPrecision(10, 2)
            .HasComputedColumnSql("protein * 4 + carbs * 4 + fats * 9", stored: true);

        builder.Property(food => food.Fiber).HasColumnName("fiber").HasPrecision(10, 2);

        builder.Property(food => food.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(food => food.PlatformEnforcementStatus)
            .HasColumnName("platform_enforcement_status")
            .HasMaxLength(20)
            .HasConversion(status => status.Value, value => Domain.ValueObjects.PlatformEnforcementStatus.FromString(value))
            .HasDefaultValue(Domain.ValueObjects.PlatformEnforcementStatus.Allowed)
            .IsRequired();

        builder.Property(food => food.PlatformEnforcementReason)
            .HasColumnName("platform_enforcement_reason")
            .HasMaxLength(50)
            .HasConversion(reason => reason == null ? null : reason.Value,
                value => value == null ? null : Domain.ValueObjects.PlatformEnforcementReason.FromString(value));

        builder.Property(food => food.PlatformEnforcedAt)
            .HasColumnName("platform_enforced_at");

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
        builder.HasIndex(food => new { food.Description, food.Name })
            .HasMethod("GIN")
            .HasOperators("gin_trgm_ops", "gin_trgm_ops")
            .HasDatabaseName("idx_foods_search_trgm");

        builder.HasOne<User>().WithMany().HasForeignKey(food => food.OwnerTrainerId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_foods_owner_trainer");
    }
}
