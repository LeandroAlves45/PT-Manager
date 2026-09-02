using Application.Features.Clients.Dtos;

namespace Api.Contracts.Clients;

/// <summary>Dados para criar uma ficha de cliente no tenant autenticado.</summary>
public sealed record CreateClientRequest(
    string Name,
    string? ContactEmail,
    string Phone,
    DateOnly BirthDate,
    string Sex,
    string? Objective,
    string? Notes,
    string? EmergencyContactName,
    string? EmergencyContactPhone);

/// <summary>Substitui o perfil editável do cliente.</summary>
public sealed record UpdateClientRequest(
    string Name,
    string? ContactEmail,
    string Phone,
    DateOnly BirthDate,
    string Sex,
    string? Objective,
    string? Notes,
    string? EmergencyContactName,
    string? EmergencyContactPhone);

/// <summary>Pack com saldo utilizavél associado á ficha.</summary>
public sealed record UsableClientPackResponse(
    Guid Id,
    Guid PackTypeId,
    string Name,
    int SessionsTotal,
    int SessionsRemaining,
    int PriceCents,
    string Currency,
    DateOnly PurchaseDate,
    DateOnly? ExpectedEndDate,
    DateTime CreatedAt)
{
    /// <summary>Projeta o snapshot da Application.</summary>
    public static UsableClientPackResponse From(UsableClientPackDto pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        return new(
            pack.Id,
            pack.PackTypeId,
            pack.Name,
            pack.SessionsTotal,
            pack.SessionsRemaining,
            pack.PriceCents,
            pack.Currency,
            pack.PurchaseDate,
            pack.ExpectedEndDate,
            pack.CreatedAt
        );
    }
}

/// <summary>Detalhe completo da ficha para o personal trainer.</summary>
public sealed record ClientDetailsResponse(
    Guid Id,
    Guid? UserId,
    string Name,
    string? ContactEmail,
    string Phone,
    DateOnly BirthDate,
    string Sex,
    string? Objective,
    string? Notes,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? AvatarUrl,
    bool IsActive,
    IReadOnlyList<UsableClientPackResponse> UsablePacks,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>Projeta o detalhe da Application.</summary>
    public static ClientDetailsResponse From(ClientDetailsDto client)
    {
        ArgumentNullException.ThrowIfNull(client);

        return new(
            client.Id,
            client.UserId,
            client.Name,
            client.ContactEmail,
            client.Phone,
            client.BirthDate,
            client.Sex,
            client.Objective,
            client.Notes,
            client.EmergencyContactName,
            client.EmergencyContactPhone,
            client.AvatarUrl,
            client.IsActive,
            client.UsablePacks.Select(UsableClientPackResponse.From).ToArray(),
            client.CreatedAt,
            client.UpdatedAt);
    }
}

/// <summary>Projeção compacta usada nas listagens.</summary>
public sealed record ClientSummaryResponse(
    Guid Id,
    string Name,
    string? ContactEmail,
    string Phone,
    DateOnly BirthDate,
    string Sex,
    string? Objective,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>Projeta o resumo da Application.</summary>
    public static ClientSummaryResponse From(ClientSummaryDto client)
    {
        ArgumentNullException.ThrowIfNull(client);

        return new(
            client.Id,
            client.Name,
            client.ContactEmail,
            client.Phone,
            client.BirthDate,
            client.Sex,
            client.Objective,
            client.IsActive,
            client.CreatedAt,
            client.UpdatedAt);
    }
}
