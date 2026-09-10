namespace Application.Features.Billing.Abstractions;

/// <summary>Autentica bytes exatos e normaliza apenas eventos permitidos.</summary>
public interface IPaymentWebhookAuthenticator
{
    PaymentWebhookAuthenticationOutcome Authenticate(
        ReadOnlyMemory<byte> body,
        string signatureHeader
    );
}
