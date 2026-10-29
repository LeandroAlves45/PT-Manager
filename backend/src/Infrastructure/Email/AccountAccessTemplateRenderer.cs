using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Infrastructure.Email;

/// <summary>
/// Os quatro emails do bloqueio de um personal trainer (RN-013): bloqueado e acesso reposto,
/// para o personal trainer e para os clientes. O cliente nunca vê o motivo nem a nota; o dia e
/// a hora em que o acesso acaba mostram-se no fuso do personal trainer.
/// </summary>
internal static partial class AccountAccessTemplateRenderer
{
    internal const string TrainerBlockedKind = "trainer_blocked";
    internal const string ClientBlockedKind = "client_blocked";
    internal const string TrainerRestoredKind = "trainer_restored";
    internal const string ClientRestoredKind = "client_restored";

    private const string ResourceName = "Infrastructure.Email.Templates.Notifications.account-access.html";
    private const string ParagraphStyle = "color:#444444;font-size:16px;line-height:1.6;margin:0 0 15px 0";

    private static readonly Lazy<string> Template =
        new(LoadTemplate, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly IReadOnlyDictionary<string, string> ReasonLabels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["malicious_content"] = "Conteúdo malicioso",
            ["dangerous_information"] = "Informação perigosa",
            ["deliberately_false_information"] = "Informação deliberadamente falsa",
            ["prohibited_content"] = "Conteúdo proibido"
        };

    internal static bool IsSupported(string kind) =>
        kind is TrainerBlockedKind or ClientBlockedKind or TrainerRestoredKind or ClientRestoredKind;

    /// <summary>Produz o assunto, o HTML e o texto simples de um dos quatro emails.</summary>
    /// <param name="email">Dados já resolvidos no envio.</param>
    /// <param name="loginUrl">
    /// Página de entrada. Quem ainda pode entrar recebe um botão para ela; o personal trainer
    /// bloqueado não recebe, porque já não consegue entrar.
    /// </param>
    public static RenderedNotificationEmail Render(AccountAccessEmail email, Uri? loginUrl = null)
    {
        ArgumentNullException.ThrowIfNull(email);

        var greeting = string.IsNullOrWhiteSpace(email.RecipientName)
            ? "Olá,"
            : $"Olá {email.RecipientName.Trim()},";
        // «do teu personal trainer, Nome,»: o nome fica entre vírgulas e a contração é «do».
        var ofTrainer = string.IsNullOrWhiteSpace(email.TrainerName)
            ? "do teu personal trainer"
            : $"do teu personal trainer, {email.TrainerName.Trim()},";

        var (subject, paragraphs) = email.Kind switch
        {
            TrainerBlockedKind => ("A tua conta PT Manager foi bloqueada", TrainerBlocked(email, greeting)),
            ClientBlockedKind => ("O acesso ao portal vai terminar", new[]
            {
                greeting,
                $"A conta {ofTrainer} está suspensa na plataforma. Continuas a poder usar o portal " +
                    $"até {FormatLocal(email)}. Depois disso, o acesso acaba."
            }),
            TrainerRestoredKind => ("O acesso à tua conta voltou", new[]
            {
                greeting,
                "O bloqueio da tua conta PT Manager foi levantado. Já podes entrar outra vez."
            }),
            ClientRestoredKind => ("O acesso ao portal voltou", new[]
            {
                greeting,
                $"O acesso ao portal {ofTrainer} voltou ao normal. Já podes entrar outra vez."
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(email), email.Kind, "Unsupported account access email.")
        };

        var content = new StringBuilder();
        foreach (var paragraph in paragraphs)
            content.Append("              <p style=\"").Append(ParagraphStyle).Append("\">")
                .Append(WebUtility.HtmlEncode(paragraph)).Append("</p>\n");

        var showLogin = loginUrl is not null && email.Kind != TrainerBlockedKind;
        var action = showLogin ? EmailButtonMarkup.Render(loginUrl!, "Entrar") : string.Empty;

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{title}}"] = WebUtility.HtmlEncode(subject),
            ["{{preheader}}"] = WebUtility.HtmlEncode(paragraphs[1]),
            ["{{content}}"] = content.ToString(),
            ["{{action}}"] = action
        };

        // Uma só passagem: um nome ou uma nota escritos como «{{action}}» ficam como texto.
        var html = Placeholders().Replace(
            Template.Value,
            match => values.TryGetValue(match.Value, out var value) ? value : match.Value);

        var loginLine = showLogin
            ? $"Entrar: {loginUrl!.AbsoluteUri}{Environment.NewLine}{Environment.NewLine}"
            : string.Empty;

        var text = string.Join(Environment.NewLine + Environment.NewLine, paragraphs) +
            Environment.NewLine + Environment.NewLine +
            loginLine +
            "PT Manager - email automático. Por favor, não respondas a este email.";

        return new RenderedNotificationEmail(subject, html, text);
    }

    private static string[] TrainerBlocked(AccountAccessEmail email, string greeting)
    {
        if (email.ReasonCode is null || !ReasonLabels.TryGetValue(email.ReasonCode, out var reason))
            throw new ArgumentException("A supported block reason is required.", nameof(email));

        var paragraphs = new List<string>
        {
            greeting,
            "A tua conta PT Manager foi bloqueada pela equipa da plataforma e deixaste de poder entrar.",
            $"Motivo: {reason}."
        };
        if (!string.IsNullOrWhiteSpace(email.Note))
            paragraphs.Add($"Nota: {email.Note.Trim()}");

        // Sem clientes que possam entrar, o email só traz o motivo e a nota.
        if (email.ClientAccessEndsAtLocal.HasValue)
            paragraphs.Add(
                $"Os teus clientes continuam a entrar no portal até {FormatLocal(email)}. " +
                "Depois disso, o acesso deles também acaba.");

        return [.. paragraphs];
    }

    private static string FormatLocal(AccountAccessEmail email)
    {
        if (!email.ClientAccessEndsAtLocal.HasValue || string.IsNullOrWhiteSpace(email.TimezoneId))
            throw new ArgumentException("The local end of client access is required.", nameof(email));

        var local = email.ClientAccessEndsAtLocal.Value;
        return $"{local:dd/MM/yyyy} às {local:HH:mm} ({email.TimezoneId})";
    }

    [GeneratedRegex("\\{\\{[a-z_]+\\}\\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholders();

    private static string LoadTemplate()
    {
        var assembly = typeof(AccountAccessTemplateRenderer).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded template '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        if (!content.Contains("{{title}}", StringComparison.Ordinal) ||
            !content.Contains("{{content}}", StringComparison.Ordinal) ||
            !content.Contains("{{preheader}}", StringComparison.Ordinal) ||
            !content.Contains("{{action}}", StringComparison.Ordinal))
            throw new InvalidOperationException("Account access template is missing placeholders.");

        return content;
    }
}

/// <summary>Dados de um email de acesso já resolvidos no envio.</summary>
/// <param name="Kind">Um dos quatro tipos.</param>
/// <param name="RecipientName">Nome de quem recebe, se existir.</param>
/// <param name="TrainerName">Nome do personal trainer, mostrado ao cliente.</param>
/// <param name="ReasonCode">Motivo do bloqueio; só no email do personal trainer bloqueado.</param>
/// <param name="Note">Nota livre do superuser; só no email do personal trainer bloqueado.</param>
/// <param name="ClientAccessEndsAtLocal">Fim do acesso dos clientes, já no fuso do personal trainer.</param>
/// <param name="TimezoneId">Fuso do personal trainer.</param>
internal sealed record AccountAccessEmail(
    string Kind,
    string? RecipientName,
    string? TrainerName,
    string? ReasonCode,
    string? Note,
    DateTime? ClientAccessEndsAtLocal,
    string? TimezoneId);
