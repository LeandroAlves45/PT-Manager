namespace Application.Features.Authentication.Dtos;

/// <summary>Segredo anti-CSRF bruto devolvido ao cliente que já possui a sessão.</summary>
public sealed record CsrfTokenDto(string RawCsrfToken);
