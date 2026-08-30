namespace Infrastructure.Identity;

/// <summary>Configuração validada do adapter de email transacional.</summary>
public sealed class ResendOptions
{
    public const string SectionName = "Resend";
    public Uri BaseAddress { get; set; } = new("https://api.resend.com/");
    public string ApiKey { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public Uri? FrontendBaseUrl { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(FromAddress) &&
        FrontendBaseUrl is not null &&
        FrontendBaseUrl.IsAbsoluteUri &&
        BaseAddress.IsAbsoluteUri &&
        Timeout > TimeSpan.Zero;
}
