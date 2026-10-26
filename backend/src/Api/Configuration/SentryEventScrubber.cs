using System.Text.RegularExpressions;

namespace Api.Configuration;

/// <summary>Última barreira antes de um evento ou breadcrumb sair para o Sentry.</summary>
/// <remarks>
/// <para>
/// <c>SendDefaultPii = false</c> já tira cookies, IP e utilizador, e
/// <c>MaxRequestBodySize = None</c> impede a leitura do corpo (o payload Stripe incluído).
/// Ficam os headers, que levam <c>Authorization</c>, <c>X-CSRF-Token</c>,
/// <c>Stripe-Signature</c> e <c>Upstash-Signature</c>, e a query, que pode levar tokens
/// (confirmação de email, reposição da palavra-passe).
/// </para>
/// <para>
/// Os breadcrumbs são os logs anteriores ao erro. O provider do Sentry tem uma regra de
/// filtro própria que ganha ao <c>Microsoft.AspNetCore: Warning</c> do appsettings, e o log
/// de framework «Request starting» traz o URL com a query. Daí a limpeza de toda a query
/// dentro das mensagens e dos dados de cada breadcrumb.
/// </para>
/// </remarks>
internal static partial class SentryEventScrubber
{
    private const string RedactedQuery = "?[redacted]";

    public static SentryEvent Scrub(SentryEvent sentryEvent)
    {
        ArgumentNullException.ThrowIfNull(sentryEvent);

        sentryEvent.Request.Headers.Clear();
        sentryEvent.Request.QueryString = null;
        return sentryEvent;
    }

    public static Breadcrumb ScrubBreadcrumb(Breadcrumb breadcrumb)
    {
        ArgumentNullException.ThrowIfNull(breadcrumb);

        // O SDK não expõe um construtor com timestamp; o BeforeBreadcrumb corre quando o
        // breadcrumb nasce, por isso a hora do novo coincide com a do original.
        return new Breadcrumb(
            WithoutQuery(breadcrumb.Message) ?? string.Empty,
            breadcrumb.Type ?? string.Empty,
            breadcrumb.Data?.ToDictionary(pair => pair.Key, pair =>
                WithoutQuery(pair.Value) ?? string.Empty),
            breadcrumb.Category,
            breadcrumb.Level);
    }

    // Uma query termina no primeiro espaço ou aspa: cobre URLs soltos dentro de uma frase.
    [GeneratedRegex("""\?[^\s"']*""")]
    private static partial Regex QueryPattern();

    private static string? WithoutQuery(string? text) =>
        text is null ? null : QueryPattern().Replace(text, RedactedQuery);
}
