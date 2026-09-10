namespace Application.Features.Billing.Abstractions;

/// <summary>Resultado de uma escrita condicional da intenção.</summary>
public enum CheckoutMutationStatus
{
    Applied,
    AlreadyApplied,
    NotFound,
    LeaseLost,
    CustomerConflict,
    ConcurrencyConflict
}
