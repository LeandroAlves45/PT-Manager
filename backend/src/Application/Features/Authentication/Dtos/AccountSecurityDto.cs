namespace Application.Features.Authentication.Dtos;

/// <summary>Estado de autenticação da conta: email, role e métodos de entrada disponíveis.</summary>
public sealed record AccountSecurityDto(
    string Email,
    string Role,
    bool HasPassword,
    bool GoogleLinked);
