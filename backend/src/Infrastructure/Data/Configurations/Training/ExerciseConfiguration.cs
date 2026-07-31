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
        builder.ToTable("exercises");
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

        builder.Property(e => e.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

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

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.OwnerTrainerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
