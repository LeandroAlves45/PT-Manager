namespace Application.Features.Authentication.RefreshSession;

/// <summary>Refresh token bruto recebido pela fronteira segura.</summary>
public sealed record RefreshSessionCommand(string RawToken, string RawCsrfToken);
