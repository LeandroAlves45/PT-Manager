using Application.Errors;

namespace Application.Features.Clients;

/// <summary>Erros estáveis dos casos de uso de Clients.</summary>
public static class ClientErrors
{
    /// <summary>Cliente inexistente ou invisível no tenant.</summary>
    public static readonly Error ClientNotFound = Error.Create(
        code: "client_not_found",
        category: ErrorCategory.NotFound,
        description: "Client was not found."
    );

    /// <summary>Email já usado por um cliente não eliminado no tenant.</summary>
    public static readonly Error ClientDuplicateEmail = Error.Create(
        code: "client_email_already_exists",
        category: ErrorCategory.Conflict,
        description: "A client with this email already exists."
    );

    /// <summary>Telefone já usado por um cliente não eliminado no tenant.</summary>
    public static readonly Error ClientDuplicatePhone = Error.Create(
        code: "client_phone_already_exists",
        category: ErrorCategory.Conflict,
        description: "A client with this phone already exists."
    );

    /// <summary>Subscrição inativa bloqueia novos clientes ativos.</summary>
    public static readonly Error SubscriptionInactive = Error.Create(
        code: "subscription_inactive",
        category: ErrorCategory.PaymentRequired,
        description: "The subscription is inactive."
    );

    /// <summary>Subscrição suspensa bloqueia novos clientes ativos.</summary>
    public static readonly Error SubscriptionSuspended = Error.Create(
        code: "subscription_suspended",
        category: ErrorCategory.PaymentRequired,
        description: "The subscription is suspended."
    );

    /// <summary>Subscrição cancelada bloqueia novos clientes ativos.</summary>
    public static readonly Error SubscriptionCancelled = Error.Create(
        code: "subscription_cancelled",
        category: ErrorCategory.PaymentRequired,
        description: "The subscription is cancelled."
    );

    /// <summary>O tier atual atingiu o limite de clientes ativos.</summary>
    public static readonly Error ClientLimitReached = Error.Create(
        code: "client_limit_reached",
        category: ErrorCategory.PaymentRequired,
        description: "The client limit for the current subscription was reached.",
        metadata: new Dictionary<string, object?>
        {
            { "upgrade", true }
        }
    );

    public static readonly Error ClientInactive = Error.Create(
        code: "client_inactive",
        category: ErrorCategory.Conflict,
        description: "An archived client cannot receive a new session pack."
    );
}
