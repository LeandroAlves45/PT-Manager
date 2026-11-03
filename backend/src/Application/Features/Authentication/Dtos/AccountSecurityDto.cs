namespace Application.Features.Authentication.Dtos;

/// <summary>
/// Estado de autenticação da conta: email, role, métodos de entrada disponíveis e o nome,
/// que o menu de perfil usa para as iniciais. O nome é nulo numa conta criada sem ele.
/// </summary>
public sealed record AccountSecurityDto(
    string Email,
    string Role,
    bool HasPassword,
    bool GoogleLinked,
    string? FullName);
