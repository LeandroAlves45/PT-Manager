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
        builder.ToTable("supplements", table =>
        {
            table.HasCheckConstraint("ck_supplements_name", "btrim(name) <> ''");
            table.HasCheckConstraint("ck_supplements_unit", "btrim(unit_of_measure) <> ''");
            table.HasCheckConstraint("ck_supplements_serving_size", "btrim(serving_size) <> ''");
            table.HasCheckConstraint("ck_supplements_timing", "btrim(timing) <> ''");
        });

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(s => s.OwnerTrainerId)
            .HasColumnName("owner_trainer_id");

        builder.Property(s => s.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(s => s.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasColumnName("description");

        builder.Property(s => s.UnitOfMeasure)
            .HasColumnName("unit_of_measure")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.ServingSize)
            .HasColumnName("serving_size")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.Timing)
            .HasColumnName("timing")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(s => s.TrainerNotes)
            .HasColumnName("trainer_notes");

        builder.Property(s => s.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(s => new
        { s.OwnerTrainerId, s.IsActive, s.Name, s.Id })
            .HasDatabaseName("idx_supplements_scope_active_name_id");
        builder.HasIndex(s => new { s.Description, s.Name })
            .HasMethod("GIN")
            .HasOperators("gin_trgm_ops", "gin_trgm_ops")
            .HasDatabaseName("idx_supplements_search_trgm");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.OwnerTrainerId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_supplements_owner_trainer");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_supplements_created_by_user");
    }
}
