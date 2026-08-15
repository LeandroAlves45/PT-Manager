using Domain.Entities.Billing;
using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Billing;

/// <summary>
/// Representa a configuração da entidade PackType para o Entity Framework Core.
/// </summary>
internal sealed class PackTypeConfiguration : IEntityTypeConfiguration<PackType>
{
    public void Configure(EntityTypeBuilder<PackType> builder)
    {
        builder.ToTable("pack_types", table =>
        {
            table.HasCheckConstraint(
                "ck_pack_types_session_count_positive",
                "session_count > 0"
            );
            table.HasCheckConstraint(
                "ck_pack_types_price_non_negative",
                "price_cents >= 0"
            );
            table.HasCheckConstraint(
                "ck_pack_types_expected_duration_positive",
                "expected_duration_days IS NULL OR expected_duration_days > 0"
            );
        });

        builder.HasKey(pt => pt.Id);
        builder.Property(pt => pt.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(pt => pt.OwnerTrainerId)
            .HasColumnName("owner_trainer_id")
            .IsRequired();

        builder.Property(pt => pt.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(pt => pt.SessionCount)
            .HasColumnName("session_count")
            .IsRequired();

        builder.Property(pt => pt.PriceCents)
            .HasColumnName("price_cents")
            .IsRequired();

        builder.Property(pt => pt.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .HasDefaultValue("EUR")
            .IsRequired();

        builder.Property(pt => pt.ExpectedDurationDays)
            .HasColumnName("expected_duration_days");

        builder.Property(pt => pt.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(pt => pt.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(pt => pt.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(pt => pt.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(pt => new { pt.OwnerTrainerId, pt.Id })
            .HasDatabaseName("uq_pack_types_tenant_id")
            .IsUnique();
        builder.HasIndex(pt => new { pt.OwnerTrainerId, pt.Name })
            .HasDatabaseName("idx_pack_types_tenant_name_active")
            .HasFilter("is_active = true AND is_deleted = false");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(pt => pt.OwnerTrainerId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pack_types_owner_trainer");
    }
}
