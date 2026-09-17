namespace Application.Features.Supplements.MarkMySupplementIntake;

/// <summary>Marca como tomada hoje uma atribuição ativa do cliente autenticado.</summary>
public sealed record MarkMySupplementIntakeCommand(Guid AssignmentId);
