namespace Application.Features.Packs.PackTypes.CreatePackType;

/// <summary>Solicita a criação de um tipo de pack privado.</summary>
public sealed record CreatePackTypeCommand(
    string Name,
    int SessionCount,
    int PriceCents,
    string Currency,
    int? ExpectedDurationDays
);
