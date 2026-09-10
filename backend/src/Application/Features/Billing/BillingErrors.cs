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

    public static readonly Error OperationTierConflict = Error.Create(
        "billing_operation_tier_conflict",
        ErrorCategory.Conflict,
        "The idempotency key was already used for another tier.");

    public static readonly Error ActiveCheckoutExists = Error.Create(
        "billing_active_checkout_exists",
        ErrorCategory.Conflict,
        "An active Checkout operation already exists.");

    public static readonly Error CheckoutLeaseLost = Error.Create(
        "billing_checkout_lease_lost",
        ErrorCategory.Conflict,
        "Checkout ownership was lost. Retry with the same idempotency key.");

    public static readonly Error UseCustomerPortal = Error.Create(
        "billing_use_customer_portal",
        ErrorCategory.Conflict,
        "Manage the existing subscription in the Customer Portal.");

    public static readonly Error CheckoutNotAvailable = Error.Create(
        "billing_checkout_not_available",
        ErrorCategory.Conflict,
        "Checkout is not available for this account.");

    public static readonly Error StripeDisabled = Error.Create(
        "billing_provider_disabled",
        ErrorCategory.ExternalDependency,
        "Billing is temporarily unavailable.");

    public static readonly Error ProviderUnavailable = Error.Create(
        "billing_provider_unavailable",
        ErrorCategory.ExternalDependency,
        "The billing provider is temporarily unavailable.");

    public static readonly Error ProviderInvalidResponse = Error.Create(
        "billing_provider_invalid_response",
        ErrorCategory.ExternalDependency,
        "The billing provider returned an invalid response.");

    public static readonly Error ProviderConfigurationMismatch = Error.Create(
        "billing_provider_configuration_mismatch",
        ErrorCategory.ExternalDependency,
        "Billing configuration is inconsistent.");
}
