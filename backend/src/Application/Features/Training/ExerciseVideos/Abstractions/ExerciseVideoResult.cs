using Domain.Entities.Training;

namespace Application.Features.Training.ExerciseVideos.Abstractions;

/// <summary>Pedido confiável de registo de um upload.</summary>
public sealed record ExerciseVideoRegistration(
    ExerciseVideoCatalog Catalog,
    ExerciseVideo Video,
    Guid ActorUserId,
    DateTime CleanupScheduledAt,
    int MaxVideosPerTrainer,
    Guid CorrelationId,
    DateTime Now);

/// <summary>Estados esperados de uma operação de registo de upload.</summary>
public enum ExerciseVideoRegistrationStatus
{
    Registered,
    ExerciseNotFound,
    ExerciseInactive,
    ExerciseBlocked,
    UploadInProgress,
    QuotaExceeded
}

/// <summary>Identificação confiável da finalização de um upload.</summary>
public sealed record ExerciseVideoUploadCompletion(
    ExerciseVideoCatalog Catalog,
    Guid ExerciseId,
    Guid VideoId,
    Guid? OwnerTrainerId,
    Guid ActorUserId,
    Guid CorrelationId,
    DateTime Now);

/// <summary>Estados esperados de uma operação de transição de estado de upload.</summary>
public enum ExerciseVideoUploadTransitionStatus
{
    Applied,
    AlreadyApplied,
    NotFound,
    UploadWindowClosed,
    InvalidState
}

public sealed record ExerciseVideoUploadTransition(
    ExerciseVideoUploadTransitionStatus Status,
    ExerciseVideo? Video = null);


/// <summary>Estados esperados de uma operação de remoção de um vídeo.</summary>
public enum ExerciseVideoRemovalStatus
{
    Removed,
    NotFound
}
