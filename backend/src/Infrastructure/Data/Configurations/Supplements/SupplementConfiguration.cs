using Domain.ValueObjects;
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
            table.HasCheckConstraint("ck_supplements_timing", "timing IS NULL OR btrim(timing) <> ''");
            table.HasCheckConstraint(
                "ck_supplements_platform_enforcement",
                "(platform_enforcement_status = 'allowed' AND platform_enforcement_reason IS NULL " +
                "AND platform_enforced_at IS NULL AND platform_enforcement_note IS NULL) OR " +
                "(platform_enforcement_status = 'blocked' AND owner_trainer_id IS NOT NULL " +
                "AND platform_enforcement_reason IN ('malicious_content', 'dangerous_information', " +
                "'deliberately_false_information', 'prohibited_content') AND platform_enforced_at IS NOT NULL)");
        });

        builder.Property(s => s.PlatformEnforcementStatus)
            .HasColumnName("platform_enforcement_status")
            .HasMaxLength(20)
            .HasConversion(status => status.Value, value => PlatformEnforcementStatus.FromString(value))
            .HasDefaultValue(PlatformEnforcementStatus.Allowed)
            .IsRequired();

        builder.Property(s => s.PlatformEnforcementReason)
            .HasColumnName("platform_enforcement_reason")
            .HasMaxLength(50)
            .HasConversion(reason => reason == null ? null : reason.Value,
                value => value == null ? null : PlatformEnforcementReason.FromString(value));

        builder.Property(s => s.PlatformEnforcedAt)
            .HasColumnName("platform_enforced_at");

        builder.Property(s => s.PlatformEnforcementNote)
            .HasColumnName("platform_enforcement_note")
            .HasMaxLength(500);


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
            .HasMaxLength(255);

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
