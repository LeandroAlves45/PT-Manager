// webhook-processor.cs
// Dois passos claramente separados:
//   1. Infrastructure (a implementar) — verifica assinatura, faz parse do JSON raw da Stripe,
//      normaliza para NormalizedPaymentEvent.
//   2. Application (já existe) — ProcessPaymentWebhookHandler processa o evento já autenticado.

namespace Infrastructure.Billing;

using Application.Features.Billing.Webhooks;
using Stripe;

/// PASSO 1 — Infrastructure: verificação de assinatura + normalização (a implementar)
public interface IStripeWebhookVerifier
{
    NormalizedPaymentEvent VerifyAndNormalize(string json, string signature);
}

public sealed class StripeWebhookVerifier : IStripeWebhookVerifier
{
    private readonly string _webhookSecret;

    public StripeWebhookVerifier(IConfiguration config)
    {
        _webhookSecret = config["Stripe:WebhookSecret"]
            ?? throw new InvalidOperationException("Stripe:WebhookSecret not configured");
    }

    public NormalizedPaymentEvent VerifyAndNormalize(string json, string signature)
    {
        // Lança StripeException se a assinatura for inválida — nunca processar sem isto
        var stripeEvent = EventUtility.ConstructEvent(json, signature, _webhookSecret);

        var kind = stripeEvent.Type switch
        {
            "checkout.session.completed" => PaymentEventKind.CheckoutCompleted,
            "customer.subscription.updated" => PaymentEventKind.SubscriptionUpdated,
            "customer.subscription.deleted" => PaymentEventKind.SubscriptionDeleted,
            "invoice.payment_succeeded" => PaymentEventKind.InvoicePaymentSucceeded,
            "invoice.payment_failed" => PaymentEventKind.InvoicePaymentFailed,
            "customer.subscription.trial_will_end" => PaymentEventKind.TrialWillEnd,
            _ => PaymentEventKind.Unknown
        };

        return MapToNormalizedEvent(stripeEvent, kind);
    }

    private static NormalizedPaymentEvent MapToNormalizedEvent(Event stripeEvent, PaymentEventKind kind)
    {
        // Extrair ProviderCustomerId / ProviderSubscriptionId consoante o tipo de objeto do payload
        throw new NotImplementedException("Mapear stripeEvent.Data.Object para NormalizedPaymentEvent");
    }
}

/// PASSO 2 — Api: endpoint que liga verificação + Application handler (a implementar)
[ApiController]
[Route("webhooks")]
[AllowAnonymous]
public sealed class StripeWebhooksController : ControllerBase
{
    private readonly IStripeWebhookVerifier _verifier;
    private readonly ProcessPaymentWebhookHandler _handler;

    public StripeWebhooksController(
        IStripeWebhookVerifier verifier,
        ProcessPaymentWebhookHandler handler)
    {
        _verifier = verifier;
        _handler = handler;
    }

    [HttpPost("stripe")]
    public async Task<IActionResult> HandleAsync(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        NormalizedPaymentEvent normalized;
        try
        {
            normalized = _verifier.VerifyAndNormalize(json, signature);
        }
        catch (StripeException)
        {
            // Assinatura inválida — nunca processar, não dar 200 (Stripe não deve achar que foi aceite)
            return Unauthorized();
        }

        // ProcessPaymentWebhookHandler (Application/Features/Billing/Webhooks/) já existe,
        // já trata idempotência via IPaymentEventStore.CommitAsync
        var result = await _handler.HandleAsync(normalized, ct);

        // Sempre 200 para eventos aceites (mesmo se Result.Failure de negócio) — evita retry
        // infinito da Stripe por erros que não se resolvem sozinhos
        return Ok();
    }
}

/// Regras:
/// 1. SEMPRE verificar X-Stripe-Signature (EventUtility.ConstructEvent) antes de qualquer outra coisa
/// 2. Normalização e verificação vivem na Infrastructure — ProcessPaymentWebhookHandler nunca vê JSON raw
/// 3. Idempotência é responsabilidade do IPaymentEventStore (Event ID), não de um log à parte
/// 4. NUNCA guardar card data — estes eventos não trazem card data (é modelo de subscrição)
/// 5. Assinatura inválida → 401/erro, não silenciar; evento válido mas negócio falhou → 200 (evita retry-loop)
