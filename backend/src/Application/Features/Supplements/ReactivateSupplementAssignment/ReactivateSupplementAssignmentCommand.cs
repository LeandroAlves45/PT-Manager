namespace Application.Features.Supplements.ReactivateSupplementAssignment;

/// <summary>Identifica a atribuição de suplemento a ser reativada.</summary>
public sealed record ReactivateSupplementAssignmentCommand(Guid AssignmentId);
