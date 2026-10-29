using Application.Features.Billing.Abstractions;
using Application.Features.Billing.Webhooks;

namespace Application.Features.Billing.Abstractions;

/// <summary>Resultado sanitizado que nunca transporta raw body ou signature.</summary>
/// <param name="Status">Desfecho da autenticação.</param>
/// <param name="PaymentEvent">Evento normalizado, só quando autenticado.</param>
/// <param name="ReceivedApiVersion">
/// Versão da API do evento recusado por <see cref="PaymentWebhookAuthenticationStatus.IncompatibleApiVersion"/>.
/// Vem de um evento com assinatura válida, por isso pode ir para os logs.
/// </param>
public sealed record PaymentWebhookAuthenticationOutcome(
    PaymentWebhookAuthenticationStatus Status,
    NormalizedPaymentEvent? PaymentEvent = null,
    string? ReceivedApiVersion = null
);
