using Microsoft.Extensions.Options;

namespace Api.Configuration;

/// <summary>Escreve, lê e elimina o nonce Google com atributos simétricos.</summary>
public sealed class GoogleChallengeCookieWriter
{
    public const string CookieName = "__Secure-ptm-google-nonce";
    public const string CookiePath = "/api/v1/auth/google";

    private readonly AuthCookieOptions _authOptions;

    public GoogleChallengeCookieWriter(IOptions<AuthCookieOptions> authOptions)
    {
        ArgumentNullException.ThrowIfNull(authOptions);
        _authOptions = authOptions.Value ?? throw new ArgumentNullException(nameof(authOptions));
    }

    public void Write(HttpResponse response, string rawNonce, DateTime expiresAt)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawNonce);
        response.Cookies.Append(CookieName, rawNonce, BuildOptions(
            new DateTimeOffset(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc))));
    }

    public static string? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Cookies.TryGetValue(CookieName, out var value) &&
            !string.IsNullOrWhiteSpace(value) ? value : null;
    }

    public void Delete(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Cookies.Delete(CookieName, BuildOptions(null));
    }

    private CookieOptions BuildOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = _authOptions.SameSite,
        Path = CookiePath,
        IsEssential = true,
        Expires = expires,
        MaxAge = expires.HasValue ? expires.Value - DateTimeOffset.UtcNow : null
    };
}
