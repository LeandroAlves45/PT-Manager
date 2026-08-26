namespace Application.Features.Billing.Abstractions;

/// <summary>Resultado estável da associação transacional.</summary>
public sealed record LinkPaymentCustomerStoreResult(LinkPaymentCustomerStoreStatus Kind);
