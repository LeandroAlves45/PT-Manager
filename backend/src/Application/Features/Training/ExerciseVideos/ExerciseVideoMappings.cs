using Application.Features.Training.ExerciseVideos.Dtos;
using Domain.Entities.Training;

namespace Application.Features.Training.ExerciseVideos;

/// <summary>Mapping explícito de vídeos geridos para contratos da Application.</summary>
public static class ExerciseVideoMappings
{
    public static ExerciseVideoDto ToDto(this ExerciseVideo video)
    {
        ArgumentNullException.ThrowIfNull(video);

        return new ExerciseVideoDto(
            video.Id,
            video.ExerciseId,
            video.OwnerTrainerId.HasValue ? "private" : "global",
            video.Status.Value,
            video.ContentType,
            video.DeclaredSizeBytes,
            video.StoredSizeBytes,
            video.DurationMilliseconds,
            video.Width,
            video.Height,
            video.VideoCodec,
            video.AudioCodec,
            video.FailureCode,
            video.UploadExpiresAt,
            video.ReadyAt,
            video.CreatedAt,
            video.UpdatedAt
        );
    }
}
