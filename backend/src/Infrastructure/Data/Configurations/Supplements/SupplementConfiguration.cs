using Domain.Entities.Identity;
using Domain.Entities.Supplements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Supplements;

/// <summary>
/// Representa a configuração da entidade Supplement para o Entity Framework Core.
/// </summary>
internal sealed class SupplementConfiguration : IEntityTypeConfiguration<Supplement>
{
    public void Configure(EntityTypeBuilder<Supplement> builder)
    {
        builder.ToTable("supplements");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(s => s.OwnerTrainerId)
            .HasColumnName("owner_trainer_id");

        builder.Property(s => s.CreatedByUserId)
            .HasColumnName("created_by_user_id");

        builder.Property(s => s.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasColumnName("description");

        builder.Property(s => s.UnitOfMeasure)
            .HasColumnName("unit_of_measure")
            .HasMaxLength(50);

        builder.Property(s => s.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(s => s.Name)
            .HasDatabaseName("idx_supplements_name");

        builder.HasIndex(s => s.OwnerTrainerId)
            .HasDatabaseName("idx_supplements_trainer");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.OwnerTrainerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

    }
}
