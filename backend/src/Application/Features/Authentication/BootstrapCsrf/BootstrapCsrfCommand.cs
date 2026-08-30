namespace Application.Features.Authentication.BootstrapCsrf;

/// <summary>Refresh token bruto recebido pela fronteira segura.</summary>
public sealed record BootstrapCsrfCommand(string RawToken);
