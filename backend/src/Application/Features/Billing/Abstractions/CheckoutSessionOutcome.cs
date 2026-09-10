namespace Application.Features.Billing.Abstractions;

/// <summary>Resultado sanitizado de uma Checkout Session.</summary>
public sealed record CheckoutSessionOutcome(
    BillingGatewayStatus Status,
    string? ProviderSessionId = null,
    Uri? Url = null,
    DateTime? ExpiresAt = null);
