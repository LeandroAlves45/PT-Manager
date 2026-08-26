namespace Application.Features.Billing.Abstractions;

/// <summary>Pedido provider-neutral para abrir o Customer Portal.</summary>
public sealed record CreateCustomerPortalRequest(
    Guid TrainerId,
    Guid OperationId,
    string ProviderCustomerId,
    Uri ReturnUrl,
    string IdempotencyKey
);
