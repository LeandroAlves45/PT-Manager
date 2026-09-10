namespace Application.Features.Billing.Abstractions;

/// <summary>
/// Classificação estável que impede exceções do provider de atravessar a Infrastructure.
/// </summary>
public enum SubscriptionReconciliationStatus
{
    Success,
    Disabled,
    TransientFailure,
    NotFound,
    InvalidResponse,
    ConfigurationMismatch
}
