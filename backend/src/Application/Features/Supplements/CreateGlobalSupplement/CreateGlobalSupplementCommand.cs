namespace Application.Features.Supplements.CreateGlobalSupplement;

/// <summary>Dados de um novo suplemento global.</summary>
public sealed record CreateGlobalSupplementCommand(
    string Name,
    string? Description,
    string UnitOfMeasure,
    string ServingSize,
    string Timing,
    string? TrainerNotes
);
