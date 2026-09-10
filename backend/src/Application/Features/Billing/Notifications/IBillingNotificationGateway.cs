namespace Application.Features.Billing.Notifications;

/// <summary>Entrega email de billing sem expor Resend.</summary>
public interface IBillingNotificationGateway
{
    Task<BillingNotificationDeliveryOutcome> SendAsync(
        BillingNotificationDelivery delivery,
        CancellationToken cancellationToken);
}
