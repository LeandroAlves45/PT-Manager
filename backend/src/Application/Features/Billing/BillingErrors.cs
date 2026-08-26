using Application.Errors;

namespace Application.Features.Billing;

/// <summary>Erros funcionais estáveis de Billing.</summary>
public static class BillingErrors
{
    public static readonly Error TrainerOnly = Error.Create(
        "billing_trainer_only",
        ErrorCategory.Forbidden,
        "Only a personal trainer can perform this operation."
    );

    public static readonly Error SubscriptionNotFound = Error.Create(
        "billing_subscription_not_found",
        ErrorCategory.NotFound,
        "The personal trainer subscription was not found."
    );

    public static readonly Error CustomerNotLinked = Error.Create(
        "billing_customer_not_linked",
        ErrorCategory.Conflict,
        "A billing customer is not linked to this subscription."
    );

    public static readonly Error CustomerConflict = Error.Create(
        "billing_customer_conflict",
        ErrorCategory.Conflict,
        "A different billing customer is already linked."
    );

    public static readonly Error ExternalIdentityConflict = Error.Create(
        "billing_external_identity_conflict",
        ErrorCategory.Conflict,
        "The external billing identity is inconsistent."
    );

    public static readonly Error ReconciliationRequired = Error.Create(
        "billing_reconciliation_required",
        ErrorCategory.ExternalDependency,
        "Current billing state is required before this event can be applied."
    );

    public static readonly Error ConcurrencyConflict = Error.Create(
        "billing_concurrency_conflict",
        ErrorCategory.Conflict,
        "Billing state changed concurrently. Try again."
    );
}
