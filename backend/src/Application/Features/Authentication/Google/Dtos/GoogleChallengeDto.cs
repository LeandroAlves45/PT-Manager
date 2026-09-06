namespace Application.Features.Authentication.Google.Dtos;

/// <summary>Nonce OIDC entregue ao cliente e instante da expiração.</summary>
public sealed record GoogleChallengeDto(string Nonce, DateTime ExpiresAt);
