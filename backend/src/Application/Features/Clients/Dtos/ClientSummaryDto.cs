namespace Application.Features.Clients.Dtos;

/// <summary>Projeção compacta de listagem de clientes.</summary>
/// <param name="Id">Identificador da ficha.</param>
/// <param name="Name">Nome completo.</param>
/// <param name="ContactEmail">Email opcional.</param>
/// <param name="Phone">Telefone.</param>
/// <param name="BirthDate">Data de nascimento.</param>
/// <param name="Sex">Sexo biológico canónico.</param>
/// <param name="Objective">Objetivo opcional.</param>
/// <param name="IsActive">Estado ativo ou arquivado.</param>
/// <param name="CreatedAt">Criação UTC.</param>
/// <param name="UpdatedAt">Última alteração UTC.</param>
public sealed record ClientSummaryDto(
    Guid Id,
    string Name,
    string? ContactEmail,
    string Phone,
    DateOnly BirthDate,
    string Sex,
    string? Objective,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
