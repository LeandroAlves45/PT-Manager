namespace Api.Configuration;

/// <summary>Limites HTTP do receptor interno de jobs.</summary>
public sealed class InternalJobDispatchHttpOptions
{
    public const string SectionName = "QStash";
    public int MaximumBodySize { get; set; } = 4 * 1024;
    public bool IsValid() => MaximumBodySize is > 0 and <= 64 * 1024;
}
