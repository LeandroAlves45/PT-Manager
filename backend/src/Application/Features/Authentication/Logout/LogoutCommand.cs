namespace Application.Features.Authentication.Logout;

/// <summary>Refresh token a revogar de forma idempotente.</summary>
public sealed record LogoutCommand(string RawToken);
