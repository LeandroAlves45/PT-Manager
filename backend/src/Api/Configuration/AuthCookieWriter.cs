using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Api.Configuration;

/// <summary>Escreve, lê e elimina o cookie de refresh com atributos consistentes.</summary>
public sealed class AuthCookieWriter
{
    private readonly AuthCookieOptions _options;

    public AuthCookieWriter(IOptions<AuthCookieOptions> options) =>
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));

    /// <summary>Emite o cookie de refresh para a sessão indicada.</summary>
    public void Write(HttpResponse response, string rawRefreshToken, DateTime expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrEmpty(rawRefreshToken);

        response.Cookies.Append(
            AuthCookieOptions.RefreshCookieName,
            rawRefreshToken,
            BuildOptions(new DateTimeOffset(
                DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc))));
    }

    /// <summary>Lê o cookie de refresh, devolvendo null quando ausente.</summary>
    public static string? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Cookies.TryGetValue(AuthCookieOptions.RefreshCookieName, out var value)
            && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    /// <summary>Elimina o cookie de refresh.</summary>
    public void Delete(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Delete(
            AuthCookieOptions.RefreshCookieName,
            BuildOptions(expires: null));
    }

    private CookieOptions BuildOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = _options.SameSite,
        Path = AuthCookieOptions.RefreshCookiePath,
        // Domain deliberadamente omitido: torna o cookie host-only
        IsEssential = true,
        Expires = expires
    };
}
