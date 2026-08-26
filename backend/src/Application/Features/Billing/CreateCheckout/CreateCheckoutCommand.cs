namespace Application.Features.Billing.CreateCheckout;

/// <summary>Entrada local para iniciar o Checkout.</summary>
public sealed record CreateCheckoutCommand(
    Guid OperationId,
    string Tier,
    Uri SuccessUrl,
    Uri CancelUrl
);
