namespace Application.Features.Billing.Notifications;

/// <summary>Classificação provider-neutral da entrega de email de billing.</summary>
public enum BillingNotificationDeliveryStatus
{
    Sent,
    TransientFailure,
    PermanentFailure
}
