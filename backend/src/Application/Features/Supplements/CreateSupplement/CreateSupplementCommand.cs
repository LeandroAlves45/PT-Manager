namespace Application.Features.Supplements.CreateSupplement;

/// <summary>Dados editáveis de um novo suplemento privado.</summary>
public sealed record CreateSupplementCommand(
    string Name,
    string? Description,
    string UnitOfMeasure,
    string ServingSize,
    string Timing,
    string? TrainerNotes
);
