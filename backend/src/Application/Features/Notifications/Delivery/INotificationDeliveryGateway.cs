namespace Application.Features.Notifications.Delivery;

/// <summary>Entrega notificações de negócio através do provider configurado.</summary>
public interface INotificationDeliveryGateway
{
    /// <summary>Entrega com uma chave estável que torna retries idempotentes.</summary>
    Task<NotificationDeliveryOutcome> SendAsync(
        NotificationDeliveryMessage message,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
