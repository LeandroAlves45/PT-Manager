namespace Application.Features.Notifications.Abstractions;

/// <summary>Classificação persistente do enqueue de notificações.</summary>
public enum NotificationQueueStoreStatus
{
    Queued,
    AlreadyQueued,
    ClientNotFound
}

/// <summary>Resultado persistente do enqueue atómico.</summary>
public sealed record NotificationQueueStoreResult(
    NotificationQueueStoreStatus Kind,
    Guid? NotificationId,
    DateTime? QueuedAt
)
{
    public static NotificationQueueStoreResult Queued(Guid id, DateTime at) =>
        new(NotificationQueueStoreStatus.Queued, id, at);

    public static NotificationQueueStoreResult AlreadyQueued(Guid id, DateTime at) =>
        new(NotificationQueueStoreStatus.AlreadyQueued, id, at);

    public static NotificationQueueStoreResult ClientNotFound() =>
        new(NotificationQueueStoreStatus.ClientNotFound, null, null);
}
