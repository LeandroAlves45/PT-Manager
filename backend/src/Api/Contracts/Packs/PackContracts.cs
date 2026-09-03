using Application.Features.Packs.ClientSessionPacks.Dtos;
using Application.Features.Packs.PackTypes.Dtos;

namespace Api.Contracts.Packs;

/// <summary>Define um tipo de pack comercializado pelo personal trainer.</summary>
public sealed record CreatePackTypeRequest(
    string Name,
    int SessionCount,
    int PriceCents,
    string Currency,
    int? ExpectedDurationDays);

/// <summary>Substitui os campos editáveis de um tipo de pack.</summary>
public sealed record UpdatePackTypeRequest(
    string Name,
    int SessionCount,
    int PriceCents,
    string Currency,
    int? ExpectedDurationDays);

/// <summary>Tipo de pack tal como devolvido ao personal trainer.</summary>
public sealed record PackTypeResponse(
    Guid Id,
    string Name,
    int SessionCount,
    int PriceCents,
    string Currency,
    int? ExpectedDurationDays,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>Projeta o DTO da Application.</summary>
    public static PackTypeResponse From(PackTypeDto packType)
    {
        ArgumentNullException.ThrowIfNull(packType);

        return new(
            packType.Id,
            packType.Name,
            packType.SessionCount,
            packType.PriceCents,
            packType.Currency,
            packType.ExpectedDurationDays,
            packType.IsActive,
            packType.CreatedAt,
            packType.UpdatedAt);
    }
}

/// <summary>Atribui um pack a um cliente do tenant.</summary>
public sealed record AssignClientSessionPackRequest(
    Guid ClientId,
    Guid PackTypeId,
    DateOnly PurchaseDate,
    DateOnly? ExpectedEndDate);

/// <summary>Ajusta a data prevista de conclusão, ou remove-a quando nulo.</summary>
public sealed record UpdateClientSessionPackExpectedEndDateRequest(DateOnly? ExpectedEndDate);

/// <summary>Pack atribuído a um cliente e o respectivo saldo.</summary>
public sealed record ClientSessionPackResponse(
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
    DateTime UpdatedAt)
{
    /// <summary>Projeta o DTO da Application no contrato da Api.</summary>
    public static ClientSessionPackResponse From(ClientSessionPackDto pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        return new(
            pack.Id,
            pack.ClientId,
            pack.PackTypeId,
            pack.PackName,
            pack.SessionsTotal,
            pack.SessionsRemaining,
            pack.PriceCents,
            pack.Currency,
            pack.PurchaseDate,
            pack.ExpectedEndDate,
            pack.IsCompleted,
            pack.CompletedAt,
            pack.IsDeleted,
            pack.CreatedAt,
            pack.UpdatedAt);
    }
}
