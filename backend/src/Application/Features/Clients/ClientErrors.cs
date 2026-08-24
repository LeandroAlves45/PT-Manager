using Application.Errors;

namespace Application.Features.Clients;

/// <summary>Erros estáveis dos casos de uso de Clients.</summary>
public static class ClientErrors
{
    public static readonly Error ClientNotFound = Error.Create(
        "client_not_found",
        ErrorCategory.NotFound,
        "Client was not found."
    );

    public static readonly Error ClientDuplicateEmail = Error.Create(
        "client_email_already_exists",
        ErrorCategory.Conflict,
        "A client with this email already exists."
    );

    public static readonly Error ClientDuplicatePhone = Error.Create(
        "client_phone_already_exists",
        ErrorCategory.Conflict,
        "A client with this phone already exists."
    );

    public static readonly Error UserAlreadyHasActiveRelationship = Error.Create(
        "client_user_already_has_active_relationship",
        ErrorCategory.Conflict,
        "The user already has an active client relationship."
    );

    public static readonly Error SubscriptionInactive = Error.Create(
        "subscription_inactive",
        ErrorCategory.PaymentRequired,
        "The subscription is inactive."
    );

    public static readonly Error SubscriptionSuspended = Error.Create(
        "subscription_suspended",
        ErrorCategory.PaymentRequired,
        "The subscription is suspended."
    );

    public static readonly Error SubscriptionCancelled = Error.Create(
        "subscription_cancelled",
        ErrorCategory.PaymentRequired,
        "The subscription is cancelled."
    );

    public static readonly Error ClientLimitReached = Error.Create(
        "client_limit_reached",
        ErrorCategory.PaymentRequired,
        "The client limit for the current subscription was reached.",
        metadata: new Dictionary<string, object?>
        {
            { "upgrade", true }
        }
    );

    public static readonly Error ClientInactive = Error.Create(
        "client_inactive",
        ErrorCategory.Conflict,
        "An archived client cannot receive a new session pack."
    );

    public static readonly Error TrainerOnly = Error.Create(
        "clients_trainer_only",
        ErrorCategory.Forbidden,
        "Only a personal trainer can manage their clients."
    );

    public static Error ClientIdRequired() => Error.Validation([
        new ValidationError(
            "ClientId",
            "client_id_required",
            "Client ID is required."
        )
    ]);
}
