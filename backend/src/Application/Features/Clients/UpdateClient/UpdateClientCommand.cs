namespace Application.Features.Clients.UpdateClient;

/// <summary>
/// Substitui o perfil editável sem alterar o tenant, conta, avatar ou estado.
/// </summary>
/// <param name="ClientId">Ficha a atualizar.</param>
/// <param name="Name">Nome completo obrigatório.</param>
/// <param name="ContactEmail">Email opcional.</param>
/// <param name="Phone">Telefone obrigatório.</param>
/// <param name="BirthDate">Data de nascimento obrigatória.</param>
/// <param name="Sex">Valor canónico male ou female.</param>
/// <param name="Objective">Objetivo opcional.</param>
/// <param name="Notes">Notas opcionais.</param>
/// <param name="EmergencyContactName">Nome de emergência opcional.</param>
/// <param name="EmergencyContactPhone">Telefone de emergência opcional.</param>
public sealed record UpdateClientCommand(
    Guid ClientId,
    string Name,
    string? ContactEmail,
    string Phone,
    DateOnly BirthDate,
    string Sex,
    string? Objective,
    string? Notes,
    string? EmergencyContactName,
    string? EmergencyContactPhone
);
