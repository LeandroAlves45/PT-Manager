namespace Application.Features.Supplements.UnmarkMySupplementIntake;

/// <summary>Desmarca a toma de hoje de uma atribuição do cliente autenticado.</summary>
public sealed record UnmarkMySupplementIntakeCommand(Guid AssignmentId);
