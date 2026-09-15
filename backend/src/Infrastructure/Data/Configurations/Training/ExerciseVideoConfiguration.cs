using Domain.Entities.Identity;
using Domain.Entities.Training;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations.Training;

/// <summary>
/// Configuração de ExerciseVideo. As invariantes de estado são
/// repetidas em check constraints para que nenhuma escrita fora do Domain as viole.
/// </summary>
internal sealed class ExerciseVideoConfiguration : IEntityTypeConfiguration<ExerciseVideo>
{
    public void Configure(EntityTypeBuilder<ExerciseVideo> builder)
    {
        builder.ToTable("exercise_videos", table =>
        {
            table.HasCheckConstraint(
                "ck_exercise_videos_status",
                "status IN ('pending', 'processing', 'ready', 'rejected', 'failed')");

            table.HasCheckConstraint(
                "ck_exercise_videos_content_type",
                "content_type IN ('video/mp4', 'video/quicktime')");

            table.HasCheckConstraint(
                "ck_exercise_videos_declared_size",
                "declared_size_bytes BETWEEN 1 AND 104857600");

            table.HasCheckConstraint(
                "ck_exercise_videos_failure_code",
                "(status IN ('rejected', 'failed')) = (failure_code IS NOT NULL)");

            table.HasCheckConstraint(
                "ck_exercise_videos_upload_confirmed",
                "status NOT IN ('processing', 'ready') OR " +
                "(stored_size_bytes = declared_size_bytes AND stored_etag IS NOT NULL " +
                "AND processing_started_at IS NOT NULL)");

            table.HasCheckConstraint(
                "ck_exercise_videos_ready_metadata",
                "status <> 'ready' OR (duration_milliseconds > 0 AND width > 0 AND height > 0 " +
                "AND video_codec IS NOT NULL AND ready_at IS NOT NULL)");
        });

        builder.HasKey(video => video.Id);
        builder.Property(video => video.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(video => video.ExerciseId)
            .HasColumnName("exercise_id")
            .IsRequired();

        builder.Property(video => video.OwnerTrainerId)
            .HasColumnName("owner_trainer_id");

        builder.Property(video => video.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion(status => status.Value, value => ExerciseVideoStatus.FromString(value))
            .IsRequired();

        builder.Property(video => video.ObjectKey)
            .HasColumnName("object_key")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(video => video.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(video => video.DeclaredSizeBytes)
            .HasColumnName("declared_size_bytes")
            .IsRequired();

        builder.Property(video => video.StoredSizeBytes)
            .HasColumnName("stored_size_bytes");

        builder.Property(video => video.StoredETag)
            .HasColumnName("stored_etag")
            .HasMaxLength(128);

        builder.Property(video => video.DurationMilliseconds)
            .HasColumnName("duration_milliseconds");

        builder.Property(video => video.Width)
            .HasColumnName("width");

        builder.Property(video => video.Height)
            .HasColumnName("height");

        builder.Property(video => video.VideoCodec)
            .HasColumnName("video_codec")
            .HasMaxLength(8);

        builder.Property(video => video.AudioCodec)
            .HasColumnName("audio_codec")
            .HasMaxLength(8);

        builder.Property(video => video.FailureCode)
            .HasColumnName("failure_code")
            .HasMaxLength(100);

        builder.Property(video => video.UploadExpiresAt)
            .HasColumnName("upload_expires_at")
            .IsRequired();

        builder.Property(video => video.ProcessingStartedAt)
            .HasColumnName("processing_started_at");

        builder.Property(video => video.ReadyAt)
            .HasColumnName("ready_at");

        builder.Property(video => video.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(video => video.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(video => video.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(video => video.ObjectKey, "uq_exercise_videos_object_key")
            .IsUnique();

        // No máximo um vídeo publicado e um upload em curso por exercício. A
        // substituição cria o novo em paralelo e só troca quando fica Ready.
        builder.HasIndex(video => video.ExerciseId, "uq_exercise_videos_ready")
            .IsUnique()
            .HasFilter("status = 'ready'");

        builder.HasIndex(video => video.ExerciseId, "uq_exercise_videos_in_flight")
            .IsUnique()
            .HasFilter("status IN ('pending', 'processing')");

        // Serve a contagem da quota por personal trainer.
        builder.HasIndex(
            video => new { video.OwnerTrainerId, video.Status },
            "idx_exercise_videos_owner_status");

        builder.HasOne<Exercise>()
            .WithMany()
            .HasForeignKey(video => video.ExerciseId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("fk_exercise_videos_exercise");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(video => video.OwnerTrainerId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_exercise_videos_owner_trainer");
    }
}
