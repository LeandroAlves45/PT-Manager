namespace Infrastructure.Media.Moderation;

/// <summary>Configuração validada da moderação Google Cloud Vision SafeSearch.</summary>
public sealed class VisionOptions
{
    public const string SectionName = "Vision";

    /// <summary>Scope mínimo necessário para <c>images:annotate</c>.</summary>
    public const string Scope = "https://www.googleapis.com/auth/cloud-vision";

    public bool Enabled { get; init; }
    public string? ServiceAccountJson { get; init; }
    public Uri BaseAddress { get; init; } = new("https://vision.googleapis.com/");
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}
