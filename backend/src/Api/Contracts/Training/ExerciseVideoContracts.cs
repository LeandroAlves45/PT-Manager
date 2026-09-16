using Application.Features.Training.ExerciseVideos.Dtos;

namespace Api.Contracts.Training;

/// <summary>Pedido de autorização de upload direto de um vídeo.</summary>
public sealed record CreateExerciseVideoUploadRequest(
    string ContentType,
    long SizeBytes);

/// <summary>Estado técnico de um vídeo gerido; o identificador do objeto nunca é exposto.</summary>
public sealed record ExerciseVideoResponse(
    Guid Id,
    Guid ExerciseId,
    string Scope,
    string Status,
    string ContentType,
    long DeclaredSizeBytes,
    long? SizeBytes,
    long? DurationMilliseconds,
    int? Width,
    int? Height,
    string? VideoCodec,
    string? AudioCodec,
    string? FailureCode,
    DateTime UploadExpiresAt,
    DateTime? ReadyAt,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static ExerciseVideoResponse From(ExerciseVideoDto video)
    {
        ArgumentNullException.ThrowIfNull(video);

        return new(
            video.Id,
            video.ExerciseId,
            video.Scope,
            video.Status,
            video.ContentType,
            video.DeclaredSizeBytes,
            video.SizeBytes,
            video.DurationMilliseconds,
            video.Width,
            video.Height,
            video.VideoCodec,
            video.AudioCodec,
            video.FailureCode,
            video.UploadExpiresAt,
            video.ReadyAt,
            video.CreatedAt,
            video.UpdatedAt);
    }
}

/// <summary>
/// Instruções do upload direto: o browser envia o ficheiro com PUT para a URL e o header
/// Content-Type exatamente igual a ContentType do vídeo.
/// </summary>
public sealed record ExerciseVideoUploadInstructionsResponse(
    string Method,
    string Url,
    string ContentType,
    DateTime ExpiresAt);

/// <summary>Vídeo pendente e autorização de upload.</summary>
public sealed record ExerciseVideoUploadResponse(
    ExerciseVideoResponse Video,
    ExerciseVideoUploadInstructionsResponse Upload,
    long MaxSizeBytes)
{
    public static ExerciseVideoUploadResponse From(ExerciseVideoUploadDto upload)
    {
        ArgumentNullException.ThrowIfNull(upload);

        return new(
            ExerciseVideoResponse.From(upload.Video),
            new ExerciseVideoUploadInstructionsResponse(
                upload.UploadMethod,
                upload.UploadUrl.AbsoluteUri,
                upload.UploadContentType,
                upload.UploadExpiresAt),
            upload.MaxSizeBytes);
    }
}

/// <summary>URL assinada de curta duração para reproduzir um vídeo Ready.</summary>
public sealed record ExerciseVideoPlaybackResponse(
    Guid VideoId,
    Guid ExerciseId,
    string ContentType,
    long DurationMilliseconds,
    int Width,
    int Height,
    string PlaybackUrl,
    DateTime ExpiresAt)
{
    public static ExerciseVideoPlaybackResponse From(ExerciseVideoPlaybackDto playback)
    {
        ArgumentNullException.ThrowIfNull(playback);

        return new(
            playback.VideoId,
            playback.ExerciseId,
            playback.ContentType,
            playback.DurationMilliseconds,
            playback.Width,
            playback.Height,
            playback.PlaybackUrl.AbsoluteUri,
            playback.PlaybackExpiresAt);
    }
}
