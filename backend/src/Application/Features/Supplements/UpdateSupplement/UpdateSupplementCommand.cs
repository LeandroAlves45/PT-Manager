namespace Application.Features.Supplements.UpdateSupplement;

/// <summary>Dados completos para atualizar um suplemento privado.</summary>
public sealed record UpdateSupplementCommand(
    Guid SupplementId,
    string Name,
    string? Description,
    string UnitOfMeasure,
    string ServingSize,
    string Timing,
    string? TrainerNotes
);
