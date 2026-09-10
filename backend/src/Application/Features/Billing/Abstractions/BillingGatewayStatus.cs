namespace Application.Features.Billing.Abstractions;

/// <summary>Classificação provider-neutral das operações remotas.</summary>
public enum BillingGatewayStatus
{
    Success,
    Disabled,
    TransientFailure,
    NotFound,
    InvalidResponse,
    ConfigurationMismatch
}
