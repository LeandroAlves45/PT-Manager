namespace Application.Features.Supplements.UpdateSupplementAssignment;

/// <summary>Atualiza as instruções de uma atribuição existente.</summary>
public sealed record UpdateSupplementAssignmentCommand(
    Guid AssignmentId,
    string ServingSize,
    string Timing,
    string? TrainerNotes
);
