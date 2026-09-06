namespace Infrastructure.Identity;

/// <summary>Configuração mínima e validada do Google Identity Services (ID token).</summary>
internal sealed class GoogleOptions
{
    internal const string SectionName = "Google";

    public string ClientId { get; init; } = string.Empty;

    internal bool IsValid() =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        ClientId.Length <= 255 &&
        ClientId.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal);
}
