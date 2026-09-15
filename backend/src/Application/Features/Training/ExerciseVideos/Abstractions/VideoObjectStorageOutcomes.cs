namespace Application.Features.Training.ExerciseVideos.Abstractions;

/// <summary>Classificação independente do fornecedor.</summary>
public enum VideoStorageStatus
{
    Success,
    Disabled,
    NotFound,
    TransientFailure,
    PermanentFailure
}

/// <summary>Instruções que o browser usa para enviar o ficheiro diretamente.</summary>
public sealed record VideoUploadAuthorization(
    Uri Url,
    string Method,
    string ContentType,
    long ContentLength,
    DateTime ExpiresAt);

public sealed record VideoUploadAuthorizationOutcome(
    VideoStorageStatus Status,
    VideoUploadAuthorization? Authorization = null,
    string? FailureCode = null);

/// <summary>Metadados confirmados pelo fornecedor, nunca pelo cliente.</summary>
public sealed record VideoObjectInfo(long SizeBytes, string ETag, string? ContentType);

public sealed record VideoObjectInfoOutcome(
    VideoStorageStatus Status,
    VideoObjectInfo? Info = null,
    string? FailureCode = null);

public sealed record VideoPlaybackUrl(Uri Url, DateTime ExpiresAt);

public sealed record VideoPlaybackUrlOutcome(
    VideoStorageStatus Status,
    VideoPlaybackUrl? Playback = null,
    string? FailureCode = null);

public sealed record VideoObjectDeletionOutcome(
    VideoStorageStatus Status,
    string? FailureCode = null);
