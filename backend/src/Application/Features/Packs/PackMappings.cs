using Application.Features.Packs.PackTypes.Dtos;
using Application.Features.Packs.ClientSessionPacks.Dtos;
using Domain.Entities.Billing;

namespace Application.Features.Packs;

/// <summary>Converte entidades de Packs em contratos da Application.</summary>
public static class PackMappings
{
    /// <summary>Mapeia um tipo privado sem expor o tenant.</summary>
    public static PackTypeDto ToDto(this PackType packType)
    {
        ArgumentNullException.ThrowIfNull(packType);

        return new PackTypeDto(
            packType.Id,
            packType.Name,
            packType.SessionCount,
            packType.PriceCents,
            packType.Currency,
            packType.ExpectedDurationDays,
            packType.IsActive,
            packType.CreatedAt,
            packType.UpdatedAt
        );
    }

    /// <summary>Mapeia um pack atribuído sem expor o tenant.</summary>
    public static ClientSessionPackDto ToDto(this ClientSessionPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        return new ClientSessionPackDto(
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
            pack.UpdatedAt
        );
    }
}
