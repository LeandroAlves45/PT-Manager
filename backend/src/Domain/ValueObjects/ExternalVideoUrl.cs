using System.Collections.Frozen;

namespace Domain.ValueObjects;

/// <summary>
/// Ligação de vídeo externa de um exercício. O cliente abre-a noutro separador, por isso só se
/// aceitam endereços HTTPS do YouTube e do Vimeo, sem utilizador nem porta e com o host
/// exatamente na lista (<c>youtube.com.exemplo.pt</c> e <c>exemplo.pt/youtube.com</c> não passam).
/// Fonte única da regra: o validador usa-a ao gravar e o portal ao ler.
/// </summary>
public static class ExternalVideoUrl
{
    /// <summary>Tamanho da coluna <c>exercises.video_url</c>.</summary>
    public const int MaxLength = 500;

    private static readonly FrozenSet<string> AllowedHosts = new[]
    {
        "youtube.com",
        "www.youtube.com",
        "m.youtube.com",
        "youtu.be",
        "vimeo.com",
        "www.vimeo.com",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Endereço absoluto em HTTPS (o host ainda não é verificado).</summary>
    public static bool IsAbsoluteHttps(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    /// <summary>Endereço que o cliente pode abrir: HTTPS, até 500 caracteres e host permitido.</summary>
    public static bool IsAllowed(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= MaxLength &&
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.UserInfo.Length == 0 &&
        uri.IsDefaultPort &&
        AllowedHosts.Contains(uri.Host);

    /// <summary>
    /// O valor só se for permitido; senão null. Para respostas ao cliente: um link antigo, gravado
    /// antes desta regra, nunca chega ao portal.
    /// </summary>
    public static string? OrNull(string? value) => IsAllowed(value) ? value : null;
}
