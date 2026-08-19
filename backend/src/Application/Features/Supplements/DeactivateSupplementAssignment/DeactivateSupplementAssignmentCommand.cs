namespace Application.Features.Supplements.DeactivateSupplementAssignment;

/// <summary>Identifica a atribuição de suplemento a ser desativada.</summary>
public sealed record DeactivateSupplementAssignmentCommand(Guid AssignmentId);
