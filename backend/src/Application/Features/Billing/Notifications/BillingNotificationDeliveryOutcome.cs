namespace Application.Features.Billing.Notifications;

/// <summary>Resultado sanitizado da entrega.</summary>
public sealed record BillingNotificationDeliveryOutcome(
    BillingNotificationDeliveryStatus Status,
    string? FailureCode = null);
