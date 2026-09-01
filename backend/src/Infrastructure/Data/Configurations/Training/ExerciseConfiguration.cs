using Domain.Entities.Identity;
using Domain.Entities.Training;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Training;

/// <summary>
/// Representa a configuração da entidade Exercise para o Entity Framework Core.
/// </summary>
internal sealed class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("exercises", table =>
            table.HasCheckConstraint(
                "ck_exercises_platform_enforcement",
                "(platform_enforcement_status = 'allowed' AND platform_enforcement_reason IS NULL " +
                "AND platform_enforced_at IS NULL) OR " +
                "(platform_enforcement_status = 'blocked' AND owner_trainer_id IS NOT NULL " +
                "AND platform_enforcement_reason IS NOT NULL " +
                "AND platform_enforcement_reason IN ('malicious_content', 'dangerous_information', " +
                "'deliberately_false_information', 'prohibited_content') AND platform_enforced_at IS NOT NULL)"
            ));

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.OwnerTrainerId)
            .HasColumnName("owner_trainer_id");

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description");

        builder.Property(e => e.MuscleGroups)
            .HasColumnName("muscle_groups")
            .HasMaxLength(500);

        builder.Property(e => e.Equipment)
            .HasColumnName("equipment")
            .HasMaxLength(255);

        builder.Property(e => e.DifficultyLevel)
            .HasColumnName("difficulty_level")
            .HasMaxLength(50);

        builder.Property(e => e.VideoUrl)
            .HasColumnName("video_url")
            .HasMaxLength(500);

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(e => e.PlatformEnforcementStatus)
            .HasColumnName("platform_enforcement_status")
            .HasMaxLength(20)
            .HasConversion(status => status.Value, value => Domain.ValueObjects.PlatformEnforcementStatus.FromString(value))
            .HasDefaultValue(Domain.ValueObjects.PlatformEnforcementStatus.Allowed)
            .IsRequired();

        builder.Property(e => e.PlatformEnforcementReason)
            .HasColumnName("platform_enforcement_reason")
            .HasMaxLength(50)
            .HasConversion(reason => reason == null ? null : reason.Value,
                value => value == null ? null : Domain.ValueObjects.PlatformEnforcementReason.FromString(value));

        builder.Property(e => e.PlatformEnforcedAt)
            .HasColumnName("platform_enforced_at");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(e => e.Name).HasDatabaseName("idx_exercises_name");
        builder.HasIndex(e => e.OwnerTrainerId).HasDatabaseName("idx_exercises_trainer");

        builder.HasIndex(e => new { e.Name, e.Description })
            .HasMethod("GIN")
            .IsTsVectorExpressionIndex("portuguese")
            .HasDatabaseName("idx_exercises_search");
        builder.HasIndex(e => new { e.Description, e.Name })
            .HasMethod("GIN")
            .HasOperators("gin_trgm_ops", "gin_trgm_ops")
            .HasDatabaseName("idx_exercises_search_trgm");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.OwnerTrainerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
