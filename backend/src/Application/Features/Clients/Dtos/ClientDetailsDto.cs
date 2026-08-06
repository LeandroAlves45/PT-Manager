namespace Application.Features.Clients.Dtos;

/// <summary>Detalhe canónico do cliente e de todos os pack utilizáveis.</summary>
/// <param name="Id">Identificador da ficha.</param>
/// <param name="UserId">Conta associada depois do convite.</param>
/// <param name="Name">Nome completo.</param>
/// <param name="ContactEmail">Email opcional.</param>
/// <param name="Phone">Telefone.</param>
/// <param name="BirthDate">Data de nascimento.</param>
/// <param name="Sex">Sexo biológico canónico.</param>
/// <param name="Objective">Objetivo opcional.</param>
/// <param name="Notes">Notas opcionais.</param>
/// <param name="EmergencyContactName">Nome de emergência.</param>
/// <param name="EmergencyContactPhone">Telefone de emergência.</param>
/// <param name="AvatarUrl">Avatar opcional.</param>
/// <param name="IsActive">Estado operacional.</param>
/// <param name="UsablePacks">Todos os packs utilizáveis ordenados.</param>
/// <param name="CreatedAt">Criação UTC.</param>
/// <param name="UpdatedAt">Última alteração UTC.</param>
public sealed record ClientDetailsDto(
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
    IReadOnlyList<UsableClientPackDto> UsablePacks,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
