namespace Application.Features.Clients.Dtos;

/// <summary>Snapshot de um pack atualmente utilizável.</summary>
/// <param name="Id">Identificador da compra.</param>
/// <param name="PackTypeId">Tipo que originou a compra.</param>
/// <param name="Name">Nome guardado no snapshot.</param>
/// <param name="SessionsTotal">Saldo original.</param>
/// <param name="SessionsRemaining">Saldo atual.</param>
/// <param name="PriceCents">Preço guardado em cêntimos.</param>
/// <param name="Currency">Moeda ISO 4217.</param>
/// <param name="PurchaseDate">Data de compra.</param>
/// <param name="ExpirationDate">Expiração opcional.</param>
public sealed record UsableClientPackDto(
    Guid Id,
    Guid PackTypeId,
    string Name,
    int SessionsTotal,
    int SessionsRemaining,
    int PriceCents,
    string Currency,
    DateOnly PurchaseDate,
    DateOnly? ExpirationDate
);
