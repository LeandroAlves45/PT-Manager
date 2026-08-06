namespace Application.Features.Clients.CreateClient;


/// <summary>
/// Dados canónicos necessários para criar uma ficha no tenant efetivo.
/// O personal trainer deriva exclusivamente de ITenantContext.
/// </summary>
/// <param name="Name">Nome completo obrigatório.</param>
/// <param name="ContactEmail">Email de contacto opcional.</param>
/// <param name="Phone">Telefone obrigatório.</param>
/// <param name="BirthDate">Data de nascimento obrigatória.</param>
/// <param name="Sex">Valor canónico male ou female.</param>
/// <param name="Objective">Objetivo opcional.</param>
/// <param name="Notes">Notas profissionais opcionais.</param>
/// <param name="EmergencyContactName">Nome de emergência opcional.</param>
/// <param name="EmergencyContactPhone">Telefone de emergência opcional.</param>
public sealed record CreateClientCommand(
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
