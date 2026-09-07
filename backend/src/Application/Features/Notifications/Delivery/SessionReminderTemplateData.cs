using System.Text.Json.Serialization;

namespace Application.Features.Notifications.Delivery;

/// <summary>Dados permitidos no template de lembrete de sessão.</summary>
public sealed record SessionReminderTemplateData(
    [property: JsonPropertyName("client_name")] string ClientName,
    [property: JsonPropertyName("trainer_name")] string TrainerName,
    [property: JsonPropertyName("session_starts_at_utc")] DateTimeOffset SessionStartsAtUtc,
    [property: JsonPropertyName("trainer_timezone")] string TrainerTimezone);
