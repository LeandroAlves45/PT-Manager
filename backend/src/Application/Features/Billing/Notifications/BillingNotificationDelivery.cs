namespace Application.Features.Billing.Notifications;

/// <summary>Dados mínimos validados para uma notificação de billing.</summary>
public sealed record BillingNotificationDelivery(
    string RecipientEmail,
    string Kind,
    Uri BillingManagementUrl,
    string IdempotencyKey);
