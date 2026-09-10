using System.Net;
using System.Text.RegularExpressions;

namespace Infrastructure.Email;

/// <summary>Renderiza templates de billing fechados em HTML e texto simples.</summary>
internal static partial class BillingNotificationTemplateRenderer
{
    internal const string PaymentFailedKind = "payment_failed";
    internal const string TrialWillEndKind = "trial_will_end";
    internal const string SubscriptionActivatedKind = "subscription_activated";
    internal const string PaymentSucceededKind = "payment_succeeded";
    internal const string SubscriptionCanceledKind = "subscription_canceled";

    private const string ResourcePrefix = "Infrastructure.Email.Templates.Notifications.";
    private const string ManagementUrlPlaceholder = "{{billing_management_url}}";

    private static readonly string[] RequiredPlaceholders = [ManagementUrlPlaceholder];

    private static readonly IReadOnlyDictionary<string, BillingTemplateDefinition> Definitions =
        new Dictionary<string, BillingTemplateDefinition>(StringComparer.Ordinal)
        {
            [PaymentFailedKind] = new(
                "payment-failed.html",
                "Pagamento da subscrição requer atenção",
                "Tentámos cobrar a tua subscrição PT Manager, mas o pagamento não foi confirmado.",
                "Gerir faturação"),
            [TrialWillEndKind] = new(
                "trial-will-end.html",
                "O período experimental termina em breve",
                "O teu trial do PT Manager está quase a acabar. Adiciona um método de pagamento para continuares sem interrupções.",
                "Gerir faturação"),
            [SubscriptionActivatedKind] = new(
                "subscription-activated.html",
                "Subscrição PT Manager activa",
                "A tua subscrição PT Manager está activa. Já podes usar a plataforma com todas as funcionalidades do teu plano.",
                "Ver faturação"),
            [PaymentSucceededKind] = new(
                "payment-succeeded.html",
                "Pagamento da subscrição confirmado",
                "Confirmámos o pagamento da tua subscrição PT Manager. A tua conta está em dia.",
                "Ver faturação"),
            [SubscriptionCanceledKind] = new(
                "subscription-canceled.html",
                "Subscrição PT Manager cancelada",
                "A tua subscrição PT Manager foi cancelada.",
                "Gerir faturação")
        };

    public static RenderedBillingEmail Render(string kind, Uri managementUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(managementUrl);

        if (!managementUrl.IsAbsoluteUri || managementUrl.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Billing management URL must use HTTPS.", nameof(managementUrl));

        if (!Definitions.TryGetValue(kind, out var definition))
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unsupported billing notification kind.");

        var encodedUrl = WebUtility.HtmlEncode(managementUrl.AbsoluteUri);
        var html = LoadTemplate(definition.ResourceName)
            .Replace(ManagementUrlPlaceholder, encodedUrl, StringComparison.Ordinal);

        if (UnresolvedPlaceholders().IsMatch(html))
            throw new InvalidOperationException("Billing template contains an unresolved placeholder.");

        var text =
            $"{definition.TextLead}{Environment.NewLine}{Environment.NewLine}" +
            $"{definition.CtaLabel}: {managementUrl.AbsoluteUri}{Environment.NewLine}{Environment.NewLine}" +
            "PT Manager - email automático. Por favor, não respondas a este email.";

        return new RenderedBillingEmail(definition.Subject, html, text);
    }

    private static string LoadTemplate(string resourceName)
    {
        var assembly = typeof(BillingNotificationTemplateRenderer).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + resourceName)
            ?? throw new InvalidOperationException($"Embedded billing template '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);

        var content = reader.ReadToEnd();
        var missing = RequiredPlaceholders
            .Where(placeholder => !content.Contains(placeholder, StringComparison.Ordinal))
            .ToArray();

        if (missing.Length > 0)
            throw new InvalidOperationException(
                $"Billing template '{resourceName}' is missing required placeholders: {string.Join(", ", missing)}.");

        return content;
    }

    [GeneratedRegex("\\{\\{[a-z_]+\\}\\}", RegexOptions.CultureInvariant)]
    private static partial Regex UnresolvedPlaceholders();

    private sealed record BillingTemplateDefinition(
        string ResourceName,
        string Subject,
        string TextLead,
        string CtaLabel);
}

internal sealed record RenderedBillingEmail(string Subject, string Html, string Text);
