namespace Application.Features.Packs.ClientSessionPacks.AssignClientSessionPack;

/// <summary>Solicita a atribuição de um tipo de pack a um cliente.</summary>
public sealed record AssignClientSessionPackCommand(
    Guid ClientId,
    Guid PackTypeId,
    DateOnly PurchaseDate,
    DateOnly? ExpectedEndDate
);
