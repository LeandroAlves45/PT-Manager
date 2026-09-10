namespace Application.Features.Billing.Abstractions;

/// <summary>Resultado fechado da autenticação do webhook.</summary>
public enum PaymentWebhookAuthenticationStatus
{
    Authenticated,
    AuthenticatedIgnored,
    Disabled,
    InvalidSignature,
    InvalidPayload,
    IncompatibleApiVersion
}
