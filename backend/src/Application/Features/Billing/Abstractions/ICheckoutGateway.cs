namespace Application.Features.Billing.Abstractions;

/// <summary>Porta externa restrita á criação de Checkout.</summary>
public interface ICheckoutGateway
{
    Task<EnsureCustomerOutcome> EnsureCustomerAsync(
        EnsureCustomerRequest request,
        CancellationToken cancellationToken);

    Task<CheckoutSessionOutcome> CreateSessionAsync(
        CreateCheckoutRequest request,
        CancellationToken cancellationToken);

    Task<CheckoutSessionOutcome> GetSessionAsync(
        string providerSessionId,
        CancellationToken cancellationToken);
}
