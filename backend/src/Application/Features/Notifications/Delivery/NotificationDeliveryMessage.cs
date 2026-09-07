namespace Application.Features.Notifications.Delivery;

/// <summary>Notificação preparada e autorizada para entrega externa.</summary>
public sealed record NotificationDeliveryMessage(
    Guid NotificationId,
    string RecipientEmail,
    string TemplateKey,
    string? TemplateDataJson);
