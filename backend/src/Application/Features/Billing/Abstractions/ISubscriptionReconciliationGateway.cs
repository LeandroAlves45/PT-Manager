namespace Application.Features.Billing.Abstractions;

/// <summary>Obtêm o estado atual para neutralizar entrega fora de ordem.</summary>
public interface ISubscriptionReconciliationGateway
{
    Task<ProviderSubscriptionSnapshot?> GetSubscriptionSnapshotAsync(
        string? providerCustomerId,
        string? providerSubscriptionId,
        CancellationToken cancellationToken
    );
}
