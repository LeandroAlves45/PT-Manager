namespace Application.Features.Notifications.Delivery;

/// <summary>Prepara e conclui notificações sob o lease do durable job.</summary>
public interface INotificationDeliveryStore
{
    Task<NotificationDeliveryPreparation> PrepareAsync(
        Guid notificationId,
        Guid jobId,
        Guid leaseOwnerId,
        CancellationToken cancellationToken);

    Task<NotificationDeliveryMutationStatus> MarkSentAsync(
        Guid notificationId,
        Guid jobId,
        Guid leaseOwnerId,
        CancellationToken cancellationToken);

    Task<NotificationDeliveryMutationStatus> MarkFailedAsync(
        Guid notificationId,
        Guid jobId,
        Guid leaseOwnerId,
        string failureCode,
        CancellationToken cancellationToken);
}
