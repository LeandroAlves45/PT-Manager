namespace Application.Features.Billing.Abstractions;

/// <summary>Dados locais necessários para iniciar o Checkout.</summary>
public sealed record CheckoutContext(string TrainerEmail, string? ProviderCustomerId);
