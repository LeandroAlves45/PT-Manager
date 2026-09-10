using System.Security.Cryptography;
using System.Text;
using Application.Features.Billing.Abstractions;
using Application.Features.Billing.Webhooks;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Infrastructure.Payments.Stripe;

/// <summary>Valida Stripe-Signature com rotação e normaliza a allowlist fechada.</summary>
internal sealed class StripePaymentWebhookAuthenticator : IPaymentWebhookAuthenticator
{
    private readonly StripeOptions _options;

    public StripePaymentWebhookAuthenticator(IOptions<StripeOptions> options) =>
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public PaymentWebhookAuthenticationOutcome Authenticate(
        ReadOnlyMemory<byte> body,
        string signatureHeader)
    {
        if (!_options.Enabled)
            return new(PaymentWebhookAuthenticationStatus.Disabled);

        var json = Encoding.UTF8.GetString(body.Span);
        global::Stripe.Event? stripeEvent = TryConstruct(
            json,
            signatureHeader,
            _options.CurrentWebhookSecret!
        );

        if (stripeEvent is null && !string.IsNullOrWhiteSpace(_options.NextWebhookSecret))
            stripeEvent = TryConstruct(
                json,
                signatureHeader,
                _options.NextWebhookSecret
            );

        if (stripeEvent is null)
            return new(PaymentWebhookAuthenticationStatus.InvalidSignature);

        if (stripeEvent.ApiVersion != StripeOptions.ApiVersion)
            return new(PaymentWebhookAuthenticationStatus.IncompatibleApiVersion);

        var kind = stripeEvent.Type switch
        {
            "checkout.session.completed" => PaymentEventKind.CheckoutCompleted,
            "customer.subscription.updated" => PaymentEventKind.SubscriptionUpdated,
            "customer.subscription.deleted" => PaymentEventKind.SubscriptionDeleted,
            "customer.subscription.trial_will_end" => PaymentEventKind.TrialWillEnd,
            "invoice.paid" => PaymentEventKind.InvoicePaid,
            "invoice.payment_failed" => PaymentEventKind.InvoicePaymentFailed,
            _ => PaymentEventKind.Unknown
        };
        if (kind == PaymentEventKind.Unknown)
            return new(PaymentWebhookAuthenticationStatus.AuthenticatedIgnored);

        var references = Extract(stripeEvent.Data.Object);
        if (references.CustomerId is null && references.SubscriptionId is null)
            return new(PaymentWebhookAuthenticationStatus.InvalidPayload);

        return new(PaymentWebhookAuthenticationStatus.Authenticated,
            new NormalizedPaymentEvent(
                stripeEvent.Id,
                stripeEvent.Type,
                kind,
                references.CustomerId,
                references.SubscriptionId,
                null,
                CorrelationIdFor(stripeEvent.Id),
                stripeEvent.Created,
                references.CheckoutSessionId
            )
        );
    }

    private static Guid CorrelationIdFor(string eventId)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(eventId), hash);
        return new Guid(hash[..16]);
    }

    private global::Stripe.Event? TryConstruct(
        string json,
        string signature,
        string secret)
    {
        try
        {
            return EventUtility.ConstructEvent(
                json,
                signature,
                secret,
                (long)_options.SignatureTolerance.TotalSeconds,
                false);
        }
        catch (StripeException)
        {
            return null;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            return null;
        }
    }

    private static (
        string? CustomerId,
        string? SubscriptionId,
        string? CheckoutSessionId) Extract(IHasObject entity) => entity switch
        {
            Session session => (session.CustomerId, session.SubscriptionId, session.Id),
            Subscription subscription => (subscription.CustomerId, subscription.Id, null),
            Invoice invoice => (
                invoice.CustomerId,
                invoice.Parent?.SubscriptionDetails?.SubscriptionId,
                null),
            _ => (null, null, null)
        };
}
