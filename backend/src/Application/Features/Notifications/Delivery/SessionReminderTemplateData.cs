using System.Text.Json.Serialization;

namespace Application.Features.Notifications.Delivery;

/// <summary>
/// Dados permitidos nos dois templates de lembrete de sessão (cliente e personal trainer).
/// <c>session_id</c> e <c>session_local_date</c> servem para confirmar, no envio, que a
/// sessão continua marcada para esse dia.
/// </summary>
public sealed record SessionReminderTemplateData(
    [property: JsonPropertyName("session_id")] Guid SessionId,
    [property: JsonPropertyName("session_local_date")] DateOnly SessionLocalDate,
    [property: JsonPropertyName("client_name")] string ClientName,
    [property: JsonPropertyName("trainer_name")] string TrainerName,
    [property: JsonPropertyName("session_starts_at_utc")] DateTimeOffset SessionStartsAtUtc,
    [property: JsonPropertyName("trainer_timezone")] string TrainerTimezone);

/// <summary>Chaves dos templates de lembrete de sessão.</summary>
public static class SessionReminderTemplates
{
    /// <summary>Email ao cliente.</summary>
    public const string Client = "session_reminder";

    /// <summary>Email ao personal trainer.</summary>
    public const string Trainer = "session_reminder_trainer";

    /// <summary>Indica se a chave é de um lembrete de sessão.</summary>
    public static bool IsSessionReminder(string templateKey) =>
        templateKey is Client or Trainer;
}
