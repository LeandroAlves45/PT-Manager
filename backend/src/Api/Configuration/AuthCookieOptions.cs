using Microsoft.AspNetCore.Http;

namespace Api.Configuration;

/// <summary>Configuração enviroment-aware do cookie de refresh.</summary>
public sealed class AuthCookieOptions
{
    public const string SectionName = "AuthCookies";

    /// <summary>
    /// Nome canónico do cookie. O prefixo __Secure- é imposto pelo browser:
    /// só aceita o cookie sobre HTTPS e com o atributo Secure presente.
    /// </summary>
    public const string RefreshCookieName = "__Secure-ptm-refresh";

    public const string RefreshCookiePath = "/api/v1/auth";

    /// <summary>
    /// Política SameSite. Determinada pela topologia real do deployment e não
    /// pelo facto de SPA e API terem portas diferentes.
    /// Produção irá ser alterada.
    /// </summary>
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Lax;

    public bool IsValid() =>
        SameSite is SameSiteMode.Lax or SameSiteMode.Strict or SameSiteMode.None;
}
