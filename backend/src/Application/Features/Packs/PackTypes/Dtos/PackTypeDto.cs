namespace Application.Features.Packs.PackTypes.Dtos;

/// <summary>Representa um tipo de pack visível ao personal trainer proprietário.</summary>
public sealed record PackTypeDto(
    Guid Id,
    string Name,
    int SessionCount,
    int PriceCents,
    string Currency,
    int? ExpectedDurationDays,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
