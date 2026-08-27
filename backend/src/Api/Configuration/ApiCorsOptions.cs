namespace Api.Configuration;

/// <summary>Configuração da allowlist CORS da API.</summary>
public sealed class ApiCorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>Origens exatas autorizadas, sem paths nem wildcards.</summary>
    public string[] AllowedOrigins { get; init; } = [];

    /// <summary>Valida que todas as origens são HTTPS, absolutas e únicas.</summary>
    public bool HasValidOrigins()
    {
        if (AllowedOrigins.Length == 0)
            return false;

        var uniqueOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var origin in AllowedOrigins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || uri.AbsolutePath != "/"
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment)
                || !string.IsNullOrEmpty(uri.UserInfo)
                || uri.Host.Contains("*", StringComparison.Ordinal)
                || !uniqueOrigins.Add(uri.GetLeftPart(UriPartial.Authority)))
                return false;
        }

        return true;
    }
}
