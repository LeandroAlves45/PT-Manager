namespace Application.Features.Notifications.Dtos;

/// <summary>Identifica a notificação aceite pela fila durável.</summary>
public sealed record QueuedNotificationDto(
    Guid NotificationId,
    string Status,
    DateTime QueuedAt
);
