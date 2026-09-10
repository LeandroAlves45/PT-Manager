namespace Application.Features.Billing.Abstractions;

/// <summary>Resultado sanitizado da criação ou recuperação do Customer.</summary>
public sealed record EnsureCustomerOutcome(
    BillingGatewayStatus Status,
    string? ProviderCustomerId = null);
