namespace Infrastructure.Jobs.QStash;

/// <summary>Configuração validada do receptor QStash.</summary>
public sealed class QStashOptions
{
    public const string SectionName = "QStash";
    public bool Enabled { get; set; }
    public string CurrentSigningKey { get; set; } = string.Empty;
    public string NextSigningKey { get; set; } = string.Empty;
    public Uri? DestinationUrl { get; set; }

    /// <summary>Máximo de bytes aceites no raw body.</summary>
    public int MaximumBodySize { get; set; } = 4 * 1024;
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan MaximumTokenLifetime { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan ReplayRetention { get; set; } = TimeSpan.FromDays(1);

    /// <summary>Validação da configuração ativa sem exigir segredos em ambientes desligados.</summary>
    public bool IsValid()
    {
        if (MaximumBodySize is <= 0 or > 64 * 1024 ||
            ClockSkew < TimeSpan.Zero ||
            ClockSkew > TimeSpan.FromMinutes(2) ||
            MaximumTokenLifetime < TimeSpan.FromMinutes(1) ||
            MaximumTokenLifetime > TimeSpan.FromMinutes(30) ||
            ReplayRetention < TimeSpan.FromMinutes(10) ||
            ReplayRetention > TimeSpan.FromDays(7))
            return false;

        // Em produção, a configuração é obrigatória.
        if (!Enabled)
            return true;

        if (!IsSigningKeyValid(CurrentSigningKey) ||
            !IsSigningKeyValid(NextSigningKey) ||
            DestinationUrl is null ||
            !DestinationUrl.IsAbsoluteUri ||
            !string.IsNullOrEmpty(DestinationUrl.Query) ||
            !string.IsNullOrEmpty(DestinationUrl.Fragment) ||
            !string.IsNullOrEmpty(DestinationUrl.UserInfo) ||
            DestinationUrl.AbsolutePath != "/api/internal/jobs/dispatch")
            return false;

        return DestinationUrl.Scheme == Uri.UriSchemeHttps || DestinationUrl.IsLoopback;
    }

    private static bool IsSigningKeyValid(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length is >= 32 and <= 4096 &&
        !value.Any(char.IsControl);
}
