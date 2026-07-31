using Domain.Entities.Assessments;
using Domain.Entities.Clients;
using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Assessments;

/// <summary>
/// Representa a configuração da entidade CheckIn para o Entity Framework Core.
/// </summary>
internal sealed class CheckInConfiguration : IEntityTypeConfiguration<CheckIn>
{
    public void Configure(EntityTypeBuilder<CheckIn> builder)
    {
        builder.ToTable("checkins");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(c => c.OwnerTrainerId)
            .HasColumnName("owner_trainer_id")
            .IsRequired();

        builder.Property(c => c.ClientId)
            .HasColumnName("client_id")
            .IsRequired();

        builder.Property(c => c.CheckInDate)
            .HasColumnName("check_in_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(c => c.TargetDate)
            .HasColumnName("target_date")
            .HasColumnType("date");

        builder.Property(c => c.WeightKg)
            .HasColumnName("weight_kg")
            .HasPrecision(10, 2);

        builder.Property(c => c.BodyFatPercentage)
            .HasColumnName("body_fat_percentage")
            .HasPrecision(10, 2);

        builder.Property(c => c.Notes)
            .HasColumnName("notes");

        builder.Property(c => c.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("checkin_date_order", "target_date IS NULL OR target_date >= check_in_date");
            t.HasCheckConstraint("checkin_weight_positive", "weight_kg IS NULL OR weight_kg > 0");
            t.HasCheckConstraint("checkin_body_fat_range", "body_fat_percentage IS NULL OR body_fat_percentage BETWEEN 0 AND 100");
        });

        builder.HasIndex(c => c.OwnerTrainerId).HasDatabaseName("idx_checkins_trainer");
        builder.HasIndex(c => c.ClientId).HasDatabaseName("idx_checkins_client");
        builder.HasIndex(c => c.CheckInDate).HasDatabaseName("idx_checkins_date");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.OwnerTrainerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(c => new { c.OwnerTrainerId, c.ClientId })
            .HasPrincipalKey(cl => new { cl.OwnerTrainerId, cl.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_checkins_client_tenant");
    }
}
