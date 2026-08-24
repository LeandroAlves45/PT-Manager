using Application.Errors;

namespace Application.Features.Notifications;

/// <summary>Erros estáveis da feature de notificações.</summary>
public static class NotificationErrors
{
    public static readonly Error TrainerOnly = Error.Create(
        "notifications_trainer_only",
        ErrorCategory.Forbidden,
        "Only a personal trainer can enqueue this notification."
    );

    public static readonly Error ClientNotFound = Error.Create(
        "notification_client_not_found",
        ErrorCategory.NotFound,
        "The notification client was not found."
    );
}
