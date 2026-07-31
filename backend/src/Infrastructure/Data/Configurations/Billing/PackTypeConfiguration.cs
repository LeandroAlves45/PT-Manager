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
        builder.ToTable("pack_types");
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

        builder.Property(pt => pt.DurationDays)
            .HasColumnName("duration_days");

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

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("pack_session_count_positive", "session_count > 0");
            t.HasCheckConstraint("pack_price_non_negative", "price_cents >= 0");
            t.HasCheckConstraint("pack_duration_positive", "duration_days IS NULL OR duration_days > 0");
        });

        builder.HasIndex(pt => pt.OwnerTrainerId).HasDatabaseName("idx_packs_trainer");
        builder.HasIndex(pt => new { pt.OwnerTrainerId, pt.Id })
            .HasDatabaseName("uq_pack_types_tenant_id")
            .IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(pt => pt.OwnerTrainerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
