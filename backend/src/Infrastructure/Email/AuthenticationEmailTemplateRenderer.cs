using System.Net;
using System.Text.RegularExpressions;

namespace Infrastructure.Email;

/// <summary>Opções visuais opcionais para templates Auth sem alterar contrato de segredo.</summary>
internal sealed record AuthenticationEmailTemplateOptions(
    string AppName = "PT Manager",
    string? RecipientName = null,
    string? FooterAttribution = null,
    string? LogoUrl = null
);

/// <summary>Renderiza templates Auth empacotados no assembly sem expor segredos em logs.</summary>
internal static partial class AuthenticationEmailTemplateRenderer
{
    private const string ResourcePrefix = "Infrastructure.Email.Templates.Auth.";
    private static readonly string[] RequiredPlaceholders =
    [
        "{{brand_header}}",
        "{{recipient_greeting}}",
        "{{intro}}",
        "{{action_url}}",
        "{{action_label}}",
        "{{footer_attribution}}"
    ];

    private const string ExpiryPlaceholder = "{{expires_at_utc}}";
    private const string SecondaryUrlPlaceholder = "{{secondary_action_url}}";
    private const string SecondaryLabelPlaceholder = "{{secondary_action_label}}";

    /// <summary>Template sem link com expiração: avisa que o email já tem conta.</summary>
    internal const string AccountAlreadyExistsTemplate = "account-already-exists.html";

    private static readonly Lazy<IReadOnlyDictionary<string, string>> Templates =
        new(LoadTemplates, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Renderiza o template. <c>expiresAt</c> é <c>null</c> nos templates sem link com prazo e
    /// <c>secondaryAction</c> só é usado pelos templates que declaram a segunda ligação.
    /// </summary>
    internal static RenderedAuthenticationEmail Render(
        string templateName,
        string intro,
        string actionUrl,
        string actionLabel,
        DateTime? expiresAt,
        AuthenticationEmailTemplateOptions? options = null,
        (string Url, string Label)? secondaryAction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentException.ThrowIfNullOrWhiteSpace(intro);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionLabel);

        options ??= new AuthenticationEmailTemplateOptions();

        if (!Templates.Value.TryGetValue(templateName, out var template))
            throw new InvalidOperationException(
                $"Authentication email template '{templateName}' is not registered.");

        var appName = options.AppName.Trim();
        var expiresAtUtc = expiresAt?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'");
        var brandHeader = BuildBrandHeader(appName, options.LogoUrl);
        var recipientGreeting = BuildRecipientGreeting(options.RecipientName);
        var footerAttribution = string.IsNullOrWhiteSpace(options.FooterAttribution)
            ? appName
            : options.FooterAttribution.Trim();

        var html = template
            .Replace("{{brand_header}}", brandHeader, StringComparison.Ordinal)
            .Replace("{{recipient_greeting}}", recipientGreeting, StringComparison.Ordinal)
            .Replace("{{intro}}", WebUtility.HtmlEncode(intro), StringComparison.Ordinal)
            .Replace("{{action_url}}", WebUtility.HtmlEncode(actionUrl), StringComparison.Ordinal)
            .Replace("{{action_label}}", WebUtility.HtmlEncode(actionLabel), StringComparison.Ordinal)
            .Replace("{{footer_attribution}}", WebUtility.HtmlEncode(footerAttribution), StringComparison.Ordinal);

        if (expiresAtUtc is not null)
            html = html.Replace(ExpiryPlaceholder, WebUtility.HtmlEncode(expiresAtUtc), StringComparison.Ordinal);

        if (secondaryAction is { } secondary)
        {
            html = html
                .Replace(SecondaryUrlPlaceholder, WebUtility.HtmlEncode(secondary.Url), StringComparison.Ordinal)
                .Replace(SecondaryLabelPlaceholder, WebUtility.HtmlEncode(secondary.Label), StringComparison.Ordinal);
        }

        if (UnresolvedPlaceholder().IsMatch(html))
            throw new InvalidOperationException(
                $"Authentication email template '{templateName}' contains an unresolved placeholder.");

        var greetingLine = string.IsNullOrWhiteSpace(options.RecipientName)
            ? string.Empty
            : $"Olá {options.RecipientName},{Environment.NewLine}{Environment.NewLine}";

        var secondaryLine = secondaryAction is { } second
            ? $"{second.Label}: {second.Url}{Environment.NewLine}{Environment.NewLine}"
            : string.Empty;

        var expiryLine = expiresAtUtc is null
            ? string.Empty
            : $"Este link expira em {expiresAtUtc}.{Environment.NewLine}{Environment.NewLine}";

        var text =
            $"{greetingLine}{intro}{Environment.NewLine}{Environment.NewLine}" +
            $"{actionLabel}: {actionUrl}{Environment.NewLine}{Environment.NewLine}" +
            secondaryLine +
            $"Se o link não abrir, copia e cola o URL acima no browser.{Environment.NewLine}{Environment.NewLine}" +
            expiryLine +
            $"{footerAttribution} - email automatico. Por favor não responda a este email.";

        return new(html, text);
    }

    private static string BuildBrandHeader(string appName, string? logoUrl)
    {
        if (!string.IsNullOrWhiteSpace(logoUrl))
        {
            return $"""
                <img src="{WebUtility.HtmlEncode(logoUrl.Trim())}" alt="{WebUtility.HtmlEncode(appName)}"
                    style="height:40px;margin:0 auto;display:block;" />
                """;
        }

        return $"""
            <p style="color:#ffffff;font-size:20px;font-weight:600;margin:0;letter-spacing:0.5px;">
                {WebUtility.HtmlEncode(appName)}
            </p>
            """;
    }

    private static string BuildRecipientGreeting(string? recipientName)
    {
        if (string.IsNullOrWhiteSpace(recipientName))
            return string.Empty;

        return $"""
            <p style="color:#444444;font-size:16px;line-height:1.6;margin:0 0 15px 0;">
                Olá <strong>{WebUtility.HtmlEncode(recipientName.Trim())}</strong>,
            </p>
            """;
    }

    private static IReadOnlyDictionary<string, string> LoadTemplates()
    {
        var names = new[]
        {
            "confirm-email.html",
            "client-invitation.html",
            "trainer-invitation.html",
            "password-reset.html",
            AccountAlreadyExistsTemplate
        };
        var templates = new Dictionary<string, string>(StringComparer.Ordinal);
        var assembly = typeof(AuthenticationEmailTemplateRenderer).Assembly;

        foreach (var name in names)
        {
            using var stream = assembly.GetManifestResourceStream(ResourcePrefix + name)
                ?? throw new InvalidOperationException(
                    $"Embedded authentication email template '{name}' was not found.");

            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();

            // A expiração é obrigatória em todos os templates menos no que não tem link com prazo.
            var required = name == AccountAlreadyExistsTemplate
                ? RequiredPlaceholders
                : [.. RequiredPlaceholders, ExpiryPlaceholder];
            var missing = required.Where(placeholder =>
                !content.Contains(placeholder, StringComparison.Ordinal)).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException(
                    $"Authentication email template '{name}' is missing required placeholders: {string.Join(", ", missing)}.");

            templates.Add(name, content);
        }

        return templates;
    }

    [GeneratedRegex("\\{\\{[a-z_]+\\}\\}", RegexOptions.CultureInvariant)]
    private static partial Regex UnresolvedPlaceholder();
}

/// <summary>Corpos HTML e texto simples produzidos a partir do mesmo modelo de dados.</summary>
internal sealed record RenderedAuthenticationEmail(string Html, string Text);
