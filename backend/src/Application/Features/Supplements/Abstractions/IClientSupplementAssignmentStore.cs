namespace Application.Features.Supplements.Abstractions;

/// <summary>Persiste atribuições tenant-safe com locks consistentes.</summary>
public interface IClientSupplementAssignmentStore
{
    Task<ClientSupplementAssignmentStoreResult> AssignAsync(
        Guid trainerId,
        Guid clientId,
        Guid supplementId,
        string? servingSize,
        string? timing,
        string? trainerNotes,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<ClientSupplementAssignmentStoreResult> UpdateInstructionsAsync(
        Guid trainerId,
        Guid assignmentId,
        string servingSize,
        string timing,
        string? trainerNotes,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<ClientSupplementAssignmentStoreResult> SetActiveAsync(
        Guid trainerId,
        Guid assignmentId,
        bool isActive,
        DateTime now,
        CancellationToken cancellationToken
    );
}
