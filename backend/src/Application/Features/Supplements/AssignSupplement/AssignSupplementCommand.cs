namespace Application.Features.Supplements.AssignSupplement;

/// <summary>Atribui um suplemento com defaults opcionais do catálogo.</summary>
public sealed record AssignSupplementCommand(
    Guid ClientId,
    Guid SupplementId,
    string? ServingSize,
    string? Timing,
    string? TrainerNotes
);
