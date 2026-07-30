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
            .ValueGeneratedOnAdd();

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
        });

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
            .HasDefaultValue(true);

        builder.Property(mp => mp.IsArchived)
            .HasColumnName("is_archived")
            .HasDefaultValue(false);

        builder.Property(mp => mp.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.ToTable(t => t.HasCheckConstraint("date_order", "starts_date <= ends_date"));

        builder.HasIndex(mp => mp.OwnerTrainerId)
            .HasDatabaseName("idx_meal_plans_trainer");

        builder.HasIndex(mp => mp.ClientId)
            .HasDatabaseName("idx_meal_plans_client");

        builder.HasIndex(mp => new { mp.OwnerTrainerId, mp.IsActive })
            .HasDatabaseName("idx_meal_plans_trainer_active");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(mp => mp.OwnerTrainerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(mp => new { mp.OwnerTrainerId, mp.ClientId })
            .HasPrincipalKey(c => new { c.OwnerTrainerId, c.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_meal_plans_client_tenant");
    }
}
