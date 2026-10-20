namespace Application.Features.Billing.Abstractions;

/// <summary>Cancela já, sem reembolso, uma subscrição paga no provider (U42).</summary>
public interface ISubscriptionCancellationGateway
{
    /// <summary>Uma subscrição já cancelada ou inexistente no provider conta como sucesso.</summary>
    Task<BillingGatewayStatus> CancelImmediatelyAsync(
        string providerSubscriptionId,
        CancellationToken cancellationToken);
}
