namespace Application.Features.Billing.Abstractions;

/// <summary>Checkout criado pelo provider e customer usado.</summary>
public sealed record CreatedCheckout(Uri Url, string ProviderCustomerId);
