using Application.Features.Billing.Abstractions;
using Application.Features.Billing.Webhooks;

namespace Application.Features.Billing.Abstractions;

/// <summary>Resultado sanitizado que nunca transporta raw body ou signature.</summary>
public sealed record PaymentWebhookAuthenticationOutcome(
    PaymentWebhookAuthenticationStatus Status,
    NormalizedPaymentEvent? PaymentEvent = null
);
