namespace Application.Features.Notifications.Abstractions;

/// <summary>
/// Write model interno. O TrainerId só pode ser preenchido por um caso de uso que já
/// tenha validado ou establecido o tenant efectivo.
/// </summary>
public sealed record NotificationQueueRequest(
    Guid TrainerId,
    Guid? ClientId,
    string RecipientEmail,
    string NotificationType,
    string TemplateKey,
    string? TemplateDataJson,
    string OperationKey,
    Guid CorrelationId,
    DateTime ScheduledAt,
    DateTime Now
);
