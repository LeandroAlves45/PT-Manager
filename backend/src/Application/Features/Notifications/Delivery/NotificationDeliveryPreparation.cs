namespace Application.Features.Notifications.Delivery;

/// <summary>Estado observado ao preparar uma notificação para entrega.</summary>
public enum NotificationDeliveryPreparationStatus
{
    Ready,
    AlreadyDelivered,
    NotFound,
    InvalidState,
    LeaseLost,

    /// <summary>
    /// O lembrete deixou de se aplicar (sessão cancelada, apagada, noutro dia ou já hoje, ou
    /// personal trainer bloqueado). Não sai email e a chave fica livre para uma nova véspera.
    /// </summary>
    Skipped
}

/// <summary>Resultado da preparação protegida pelo lease do durable job.</summary>
public sealed record NotificationDeliveryPreparation(
    NotificationDeliveryPreparationStatus Status,
    NotificationDeliveryMessage? Message)
{
    public static NotificationDeliveryPreparation Ready(NotificationDeliveryMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new(NotificationDeliveryPreparationStatus.Ready, message);
    }

    /// <summary>Cria um resultado sem mensagem.</summary>
    public static NotificationDeliveryPreparation FromStatus(
        NotificationDeliveryPreparationStatus status)
    {
        if (status == NotificationDeliveryPreparationStatus.Ready)
            throw new ArgumentException("Ready requires a delivery message.", nameof(status));

        return new(status, null);
    }
}

/// <summary>Resultado de uma mutação da notificação condicionada pelo lease.</summary>
public enum NotificationDeliveryMutationStatus
{
    Applied,
    AlreadyApplied,
    LeaseLost,
    InvalidState
}
