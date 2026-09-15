using Application.Errors;

namespace Application.Features.Training.ExerciseVideos;

/// <summary>Erros estáveis dos casos de uso de vídeos geridos de exercício.</summary>
public static class ExerciseVideoErrors
{
    public static readonly Error VideoNotFound = Error.Create(
        "exercise_video_not_found",
        ErrorCategory.NotFound,
        "The exercise video was not found.");

    public static readonly Error UploadNotFound = Error.Create(
        "exercise_video_upload_not_found",
        ErrorCategory.NotFound,
        "The exercise video upload was not found.");

    public static readonly Error ExerciseInactive = Error.Create(
        "exercise_video_exercise_inactive",
        ErrorCategory.Conflict,
        "A video cannot be uploaded to an archived exercise.");

    public static readonly Error ExerciseBlocked = Error.Create(
        "exercise_video_exercise_blocked",
        ErrorCategory.Conflict,
        "A video cannot be uploaded to a platform-blocked exercise.");

    public static readonly Error UploadInProgress = Error.Create(
        "exercise_video_upload_in_progress",
        ErrorCategory.Conflict,
        "The exercise already has a video upload in progress.");

    public static readonly Error QuotaExceeded = Error.Create(
        "exercise_video_quota_exceeded",
        ErrorCategory.Conflict,
        "The personal trainer reached the managed video quota.");

    public static readonly Error UploadExpired = Error.Create(
        "exercise_video_upload_expired",
        ErrorCategory.Conflict,
        "The upload authorization expired. Request a new upload.");

    public static readonly Error UploadIncomplete = Error.Create(
        "exercise_video_upload_incomplete",
        ErrorCategory.Conflict,
        "The uploaded file was not found in storage. Finish the upload first.");

    public static readonly Error UploadRejected = Error.Create(
        "exercise_video_upload_rejected",
        ErrorCategory.Conflict,
        "The uploaded file does not match the upload authorization.");

    public static readonly Error UploadClosed = Error.Create(
        "exercise_video_upload_closed",
        ErrorCategory.Conflict,
        "The upload was already rejected or failed. Request a new upload.");

    public static readonly Error StateConflict = Error.Create(
        "exercise_video_state_conflict",
        ErrorCategory.Conflict,
        "The exercise video changed state concurrently.");

    public static readonly Error StorageUnavailable = Error.Create(
        "exercise_video_storage_unavailable",
        ErrorCategory.ExternalDependency,
        "Video storage is temporarily unavailable.");

    public static Error VideoIdRequired() => Error.Validation([
        new ValidationError(
            "VideoId",
            "exercise_video_id_required",
            "Video ID is required.")
    ]);
}
