namespace Application.Features.Billing.Abstractions;

/// <summary>Porta externa restrita á criação de Checkout.</summary>
public interface ICheckoutGateway
{
    Task<CreatedCheckout> CreateCheckoutAsync(
        CreateCheckoutRequest request,
        CancellationToken cancellationToken
    );
}
