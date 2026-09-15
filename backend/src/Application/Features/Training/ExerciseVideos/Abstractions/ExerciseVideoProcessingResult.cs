namespace Application.Features.Training.ExerciseVideos.Abstractions;

/// <summary>Identificação do vídeo e do lease do job que o processa.</summary>
public sealed record ExerciseVideoLease(
    Guid VideoId,
    Guid? OwnerTrainerId,
    Guid JobId,
    Guid LeaseOwnerId,
    Guid CorrelationId);

/// <summary>Estados esperados de uma operação de terminação de um job.</summary>
public enum ExerciseVideoTermination
{
    Rejected,
    Failed
}

/// <summary>Estados esperados de uma operação de transição de estado de um job.</summary>
public enum ExerciseVideoJobTransitionStatus
{
    Applied,
    AlreadyTerminal,
    InvalidState,
    LeaseLost,
    NotFound
}
