namespace Application.Features.Billing.Abstractions;

/// <summary>Resultado sanitizado de uma sessão do Customer Portal.</summary>
public sealed record CustomerPortalOutcome(BillingGatewayStatus Status, Uri? Url = null);
