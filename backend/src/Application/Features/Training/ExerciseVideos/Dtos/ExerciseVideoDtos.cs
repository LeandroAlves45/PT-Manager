namespace Application.Features.Training.ExerciseVideos.Dtos;

/// <summary>
/// Estado técnico de um vídeo para quem o gere. O identificador do objeto no
/// storage nunca é exposto.
/// </summary>
public sealed record ExerciseVideoDto(
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
    DateTime UpdatedAt);

/// <summary>Autorização de upload direto emitida para um vídeo pendente.</summary>
public sealed record ExerciseVideoUploadDto(
    ExerciseVideoDto Video,
    string UploadMethod,
    Uri UploadUrl,
    string UploadContentType,
    DateTime UploadExpiresAt,
    long MaxSizeBytes);

/// <summary>Url assinada de curta duração para reproduzir um vídeo Ready.</summary>
public sealed record ExerciseVideoPlaybackDto(
    Guid VideoId,
    Guid ExerciseId,
    string ContentType,
    long DurationMilliseconds,
    int Width,
    int Height,
    Uri PlaybackUrl,
    DateTime PlaybackExpiresAt);
