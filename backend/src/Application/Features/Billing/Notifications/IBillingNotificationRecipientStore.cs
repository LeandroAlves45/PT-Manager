namespace Application.Features.Billing.Notifications;

/// <summary>Carrega o email atual do personal trainer no momento da entrega.</summary>
public interface IBillingNotificationRecipientStore
{
    Task<string?> GetEmailAsync(
        Guid trainerId,
        CancellationToken cancellationToken);
}
