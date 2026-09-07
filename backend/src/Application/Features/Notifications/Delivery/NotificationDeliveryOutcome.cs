namespace Application.Features.Notifications.Delivery;

/// <summary>Categoria do resultado da entrega externa.</summary>
public enum NotificationDeliveryOutcomeKind
{
    Sent,
    TransientFailure,
    PermanentFailure
}

/// <summary>Resultado seguro devolvido pela porta de entrega.</summary>
public sealed record NotificationDeliveryOutcome
{
    private NotificationDeliveryOutcome(
        NotificationDeliveryOutcomeKind kind,
        string? failureCode)
    {
        Kind = kind;
        FailureCode = failureCode;
    }

    public NotificationDeliveryOutcomeKind Kind { get; }
    public string? FailureCode { get; }

    public static NotificationDeliveryOutcome Sent() =>
        new(NotificationDeliveryOutcomeKind.Sent, null);

    public static NotificationDeliveryOutcome TransientFailure(string failureCode) =>
        CreateFailure(NotificationDeliveryOutcomeKind.TransientFailure, failureCode);

    public static NotificationDeliveryOutcome PermanentFailure(string failureCode) =>
        CreateFailure(NotificationDeliveryOutcomeKind.PermanentFailure, failureCode);

    private static NotificationDeliveryOutcome CreateFailure(
        NotificationDeliveryOutcomeKind kind,
        string failureCode)
    {
        if (string.IsNullOrWhiteSpace(failureCode) || failureCode.Length > 100)
            throw new ArgumentException("Failure code is invalid.", nameof(failureCode));

        return new(kind, failureCode);
    }
}
