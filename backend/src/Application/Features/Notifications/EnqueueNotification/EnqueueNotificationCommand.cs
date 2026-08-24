namespace Application.Features.Notifications.EnqueueNotification;

/// <summary>Agenda uma notificação pertecente ao personal trainer autenticado.</summary>
public sealed record EnqueueNotificationCommand(
    Guid? ClientId,
    string RecipientEmail,
    string NotificationType,
    string TemplateKey,
    string? TemplateDataJson,
    string OperationKey,
    Guid CorrelationId,
    DateTimeOffset? ScheduledAt
);
