using Domain.Entities.Supplements;

namespace Application.Features.Supplements.Abstractions;

/// <summary>Representa outcomes esperados de uma mutação de atribuição de suplemento.</summary>
public sealed class ClientSupplementAssignmentStoreResult
{
    public enum Status
    {
        Assigned,
        Updated,
        Changed,
        AlreadyInRequestedState,
        ClientNotFound,
        ClientInactive,
        SupplementNotFound,
        SupplementInactive,
        AssignmentNotFound,
        AssignmentAlreadyExists
    }

    public Status Kind { get; }
    public ClientSupplementAssignment? Assignment { get; }
    public Supplement? Supplement { get; }

    private ClientSupplementAssignmentStoreResult(
        Status kind,
        ClientSupplementAssignment? assignment,
        Supplement? supplement
    )
    {
        Kind = kind;
        Assignment = assignment;
        Supplement = supplement;
    }

    public static ClientSupplementAssignmentStoreResult WithEntities(
        Status kind,
        ClientSupplementAssignment assignment,
        Supplement supplement
    )
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(supplement);
        return new ClientSupplementAssignmentStoreResult(kind, assignment, supplement);
    }

    public static ClientSupplementAssignmentStoreResult For(Status kind) =>
        new(kind, null, null);
}
