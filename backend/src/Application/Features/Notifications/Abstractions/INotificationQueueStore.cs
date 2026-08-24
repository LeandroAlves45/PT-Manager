namespace Application.Features.Notifications.Abstractions;

/// <summary>Persiste uma notificação e o respetivo job numa única transação.</summary>
public interface INotificationQueueStore
{
    Task<NotificationQueueStoreResult> EnqueueAsync(
        NotificationQueueRequest request,
        CancellationToken cancellationToken);
}
