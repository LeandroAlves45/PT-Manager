namespace Application.Features.Packs.PackTypes.UpdatePackType;

/// <summary>Solicita a atualização de um tipo de pack privado.</summary>
public sealed record UpdatePackTypeCommand(
    Guid PackTypeId,
    string Name,
    int SessionCount,
    int PriceCents,
    string Currency,
    int? ExpectedDurationDays
);
