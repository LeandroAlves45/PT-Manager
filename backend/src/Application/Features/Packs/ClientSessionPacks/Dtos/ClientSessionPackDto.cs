namespace Application.Features.Packs.ClientSessionPacks.Dtos;

/// <summary>Representa um tipo de pack privado.</summary>
public sealed record ClientSessionPackDto(
    Guid Id,
    Guid ClientId,
    Guid PackTypeId,
    string PackName,
    int SessionsTotal,
    int SessionsRemaining,
    int PriceCents,
    string Currency,
    DateOnly PurchaseDate,
    DateOnly? ExpectedEndDate,
    bool IsCompleted,
    DateTime? CompletedAt,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

