using Application.Features.Clients.Dtos;
using Domain.Entities.Billing;
using Domain.Entities.Clients;

namespace Application.Features.Clients;

/// <summary>Mappings explícitos entre entidades de Clients e DTOs da Application.</summary>
public static class ClientMappings
{
    /// <summary>
    /// Converte uma entidade e os packs já projetados para o detalhe canónico.
    /// </summary>
    /// <param name="client">Entidade visível no tenant efetivo.</param>
    /// <param name="usablePacks">Packs utilizáveis já ordenados.</param>
    /// <returns>DTO sem expor OwnerTrainerId ou IsDeleted.</returns>
    public static ClientDetailsDto ToDetailsDto(
        this Client client,
        IReadOnlyList<UsableClientPackDto> usablePacks)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(usablePacks);

        return new ClientDetailsDto(
            Id: client.Id,
            UserId: client.UserId,
            Name: client.Name,
            ContactEmail: client.ContactEmail,
            Phone: client.Phone,
            BirthDate: client.BirthDate.Value,
            Sex: client.Sex.Value,
            Objective: client.Objective,
            Notes: client.Notes,
            EmergencyContactName: client.EmergencyContactName,
            EmergencyContactPhone: client.EmergencyContactPhone,
            AvatarUrl: client.AvatarUrl,
            IsActive: client.IsActive,
            UsablePacks: usablePacks,
            CreatedAt: client.CreatedAt,
            UpdatedAt: client.UpdatedAt);
    }

    /// <summary>Converte um pack utilizável para o snapshot público.</summary>
    /// <param name="pack">Entidade previamente filtrada pelo saldo utilizável.</param>
    /// <returns>Snapshot comercial da compra.</returns>
    public static UsableClientPackDto ToDto(
        this ClientSessionPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        return new UsableClientPackDto(
            Id: pack.Id,
            PackTypeId: pack.PackTypeId,
            Name: pack.PackName,
            SessionsTotal: pack.SessionsTotal,
            SessionsRemaining: pack.SessionsRemaining,
            PriceCents: pack.PriceCents,
            Currency: pack.Currency,
            PurchaseDate: pack.PurchaseDate,
            ExpectedEndDate: pack.ExpectedEndDate,
            CreatedAt: pack.CreatedAt);
    }
}
