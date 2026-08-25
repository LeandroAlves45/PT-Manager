namespace Application.Features.Authentication.Abstractions;

/// <summary>Segredo emitido e mantido apenas em memória até á entrega direta.</summary>
public sealed record IssuedAuthenticationSecret(
    string RecipientEmail,
    string RawToken,
    DateTime ExpiresAt
);
