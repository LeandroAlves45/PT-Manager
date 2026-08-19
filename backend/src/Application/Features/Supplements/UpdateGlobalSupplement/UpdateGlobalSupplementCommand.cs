namespace Application.Features.Supplements.UpdateGlobalSupplement;

/// <summary>Dados completos da atualização administrativa de suplemento global.</summary>
public sealed record UpdateGlobalSupplementCommand(
    Guid SupplementId,
    string Name,
    string? Description,
    string UnitOfMeasure,
    string ServingSize,
    string Timing,
    string? TrainerNotes
);
