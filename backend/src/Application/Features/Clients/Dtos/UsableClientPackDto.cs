namespace Application.Features.Clients.Dtos;

/// <summary>Snapshot de um pack com saldo atualmente utilizável.</summary>
public sealed record UsableClientPackDto(
    Guid Id,
    Guid PackTypeId,
    string Name,
    int SessionsTotal,
    int SessionsRemaining,
    int PriceCents,
    string Currency,
    DateOnly PurchaseDate,
    DateOnly? ExpectedEndDate,
    DateTime CreatedAt
);
