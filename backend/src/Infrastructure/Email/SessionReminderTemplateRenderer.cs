using System.Net;
using System.Text.RegularExpressions;
using Application.Features.Notifications.Delivery;

namespace Infrastructure.Email;

/// <summary>Renderiza o único template de negócio.</summary>
internal static partial class SessionReminderTemplateRenderer
{
    private const string ResourceName =
        "Infrastructure.Email.Templates.Notifications.session-reminder.html";

    private static readonly string[] RequiredPlaceholders =
    [
        "{{client_name}}",
        "{{trainer_name}}",
        "{{session_local_date}}",
        "{{session_local_time}}",
        "{{trainer_timezone}}"
    ];

    private static readonly Lazy<string> Template =
        new(LoadTemplate, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Valida os dados e produz duas representações independentes.</summary>
    public static RenderedNotificationEmail Render(SessionReminderTemplateData data)
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

        var html = Template.Value
            .Replace("{{client_name}}", WebUtility.HtmlEncode(clientName), StringComparison.Ordinal)
            .Replace("{{trainer_name}}", WebUtility.HtmlEncode(trainerName), StringComparison.Ordinal)
            .Replace("{{session_local_date}}", WebUtility.HtmlEncode(localDate), StringComparison.Ordinal)
            .Replace("{{session_local_time}}", WebUtility.HtmlEncode(localTime), StringComparison.Ordinal)
            .Replace("{{trainer_timezone}}", WebUtility.HtmlEncode(timezoneId), StringComparison.Ordinal);

        if (UnresolvedPlaceholders().IsMatch(html))
            throw new InvalidOperationException("Session reminder contains an unresolved placeholder.");

        var text =
            $"Olá {clientName},{Environment.NewLine}{Environment.NewLine}" +
            $"Lembramos que tem uma sessão com {trainerName} no dia {localDate} " +
            $"às {localTime} ({timezoneId}).{Environment.NewLine}{Environment.NewLine}" +
            "PT Manager - email automático. Por favor, não responda a este email.";

        return new RenderedNotificationEmail(
            "Lembrete da sua sessão",
            html,
            text);
    }

    private static string LoadTemplate()
    {
        var assembly = typeof(SessionReminderTemplateRenderer).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                "Embedded session reminder template was not found.");

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
    private static partial Regex UnresolvedPlaceholders();
}

/// <summary>Assunto e corpos completos de uma notificação renderizada.</summary>
internal sealed record RenderedNotificationEmail(string Subject, string Html, string Text);
