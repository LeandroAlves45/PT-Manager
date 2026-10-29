using System.Net;
using System.Text.RegularExpressions;
using Application.Features.Notifications.Delivery;

namespace Infrastructure.Email;

/// <summary>
/// Renderiza os dois lembretes de sessão da véspera: o do cliente e o do personal trainer. A
/// data e a hora mostram-se no fuso do personal trainer.
/// </summary>
internal static partial class SessionReminderTemplateRenderer
{
    private const string ClientResourceName =
        "Infrastructure.Email.Templates.Notifications.session-reminder.html";
    private const string TrainerResourceName =
        "Infrastructure.Email.Templates.Notifications.session-reminder-trainer.html";

    private static readonly string[] RequiredPlaceholders =
    [
        "{{client_name}}",
        "{{trainer_name}}",
        "{{session_local_date}}",
        "{{session_local_time}}",
        "{{trainer_timezone}}",
        "{{preheader}}"
    ];

    private static readonly Lazy<string> ClientTemplate =
        new(() => LoadTemplate(ClientResourceName), LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<string> TrainerTemplate =
        new(() => LoadTemplate(TrainerResourceName), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Valida os dados e produz o assunto e as duas representações do email.</summary>
    /// <param name="data">Dados do template, iguais nas duas versões.</param>
    /// <param name="forTrainer">Verdadeiro para o email do personal trainer.</param>
    public static RenderedNotificationEmail Render(SessionReminderTemplateData data, bool forTrainer = false)
    {
        ArgumentNullException.ThrowIfNull(data);

        var clientName = ValidateText(data.ClientName, nameof(data.ClientName), 200);
        var trainerName = ValidateText(data.TrainerName, nameof(data.TrainerName), 200);
        var timezoneId = ValidateText(data.TrainerTimezone, nameof(data.TrainerTimezone), 200);

        if (data.SessionStartsAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException(
                "Session start must use the UTC offset.", nameof(data));

        var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        var localStart = TimeZoneInfo.ConvertTime(data.SessionStartsAtUtc, timezone);
        var localDate = localStart.ToString("dd/MM/yyyy");
        var localTime = localStart.ToString("HH:mm");

        var template = forTrainer ? TrainerTemplate.Value : ClientTemplate.Value;
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{client_name}}"] = WebUtility.HtmlEncode(clientName),
            ["{{trainer_name}}"] = WebUtility.HtmlEncode(trainerName),
            ["{{session_local_date}}"] = WebUtility.HtmlEncode(localDate),
            ["{{session_local_time}}"] = WebUtility.HtmlEncode(localTime),
            ["{{trainer_timezone}}"] = WebUtility.HtmlEncode(timezoneId),
            ["{{preheader}}"] = WebUtility.HtmlEncode(forTrainer
                ? $"Sessão com {clientName} amanhã, {localDate} às {localTime}."
                : $"Sessão com {trainerName} a {localDate} às {localTime}.")
        };

        // Uma só passagem sobre o template: um nome escrito como «{{x}}» entra como texto e
        // não volta a ser lido como placeholder.
        var html = Placeholders().Replace(
            template,
            match => values.TryGetValue(match.Value, out var value)
                ? value
                : throw new InvalidOperationException(
                    "Session reminder contains an unresolved placeholder."));

        const string footer = "PT Manager - email automático. Por favor, não respondas a este email.";
        var newLine = Environment.NewLine;

        return forTrainer
            ? new RenderedNotificationEmail(
                "Sessão marcada para amanhã",
                html,
                $"Olá {trainerName},{newLine}{newLine}" +
                $"Amanhã tens uma sessão marcada com {clientName} no dia {localDate} " +
                $"às {localTime} ({timezoneId}).{newLine}{newLine}{footer}")
            : new RenderedNotificationEmail(
                "Lembrete da tua sessão",
                html,
                $"Olá {clientName},{newLine}{newLine}" +
                $"Lembramos que tens uma sessão com {trainerName} no dia {localDate} " +
                $"às {localTime} ({timezoneId}).{newLine}{newLine}{footer}");
    }

    private static string LoadTemplate(string resourceName)
    {
        var assembly = typeof(SessionReminderTemplateRenderer).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded session reminder template {resourceName} was not found.");

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        var missing = RequiredPlaceholders
            .Where(placeholder => !content.Contains(placeholder, StringComparison.Ordinal))
            .ToArray();

        if (missing.Length > 0)
            throw new InvalidOperationException(
                $"Session reminder template is missing placeholders: {string.Join(", ", missing)}");

        return content;
    }

    private static string ValidateText(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maxLength)
            throw new ArgumentException("Template value is invalid.", parameterName);

        return value.Trim();
    }

    [GeneratedRegex("\\{\\{[a-z_]+\\}\\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholders();
}

/// <summary>Assunto e corpos completos de uma notificação renderizada.</summary>
internal sealed record RenderedNotificationEmail(string Subject, string Html, string Text);
