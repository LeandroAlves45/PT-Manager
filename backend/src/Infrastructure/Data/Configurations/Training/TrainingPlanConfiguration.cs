using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Domain.Entities.Training;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Training;

/// <summary>
/// Representa a configuração da entidade TrainingPlan para o Entity Framework Core.
/// </summary>
internal sealed class TrainingPlanConfiguration : IEntityTypeConfiguration<TrainingPlan>
{
    public void Configure(EntityTypeBuilder<TrainingPlan> builder)
    {
        builder.ToTable("training_plans");
        builder.HasKey(tp => tp.Id);
        builder.Property(tp => tp.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(tp => tp.OwnerTrainerId)
            .HasColumnName("owner_trainer_id")
            .IsRequired();

        builder.Property(tp => tp.ClientId)
            .HasColumnName("client_id")
            .IsRequired();

        builder.Property(tp => tp.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(tp => tp.Description)
            .HasColumnName("description");

        builder.Property(tp => tp.TrainingModality)
            .HasColumnName("training_modality")
            .HasMaxLength(50);

        builder.Property(tp => tp.Notes)
            .HasColumnName("notes");

        builder.Property(tp => tp.StartDate)
            .HasColumnName("starts_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(tp => tp.EndDate)
            .HasColumnName("ends_date")
            .HasColumnType("date");

        builder.Property(tp => tp.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(tp => tp.IsArchived)
            .HasColumnName("is_archived")
            .HasDefaultValue(false);

        builder.Property(tp => tp.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(tp => tp.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(tp => tp.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.ToTable(t => t.HasCheckConstraint("date_order", "starts_date <= ends_date"));

        builder.HasIndex(tp => tp.OwnerTrainerId).HasDatabaseName("idx_training_plans_trainer");
        builder.HasIndex(tp => tp.ClientId).HasDatabaseName("idx_training_plans_client");
        builder.HasIndex(tp => new { tp.OwnerTrainerId, tp.IsActive })
            .HasDatabaseName("idx_training_plans_trainer_active");
        builder.HasIndex(tp => new { tp.OwnerTrainerId, tp.ClientId })
            .HasDatabaseName("uq_training_plan_active_per_client")
            .HasFilter("is_active = true AND is_deleted = false")
            .IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(tp => tp.OwnerTrainerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(tp => new { tp.OwnerTrainerId, tp.ClientId })
            .HasPrincipalKey(c => new { c.OwnerTrainerId, c.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_training_plans_client_tenant");
    }
}
