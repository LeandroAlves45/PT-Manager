using System.Security.Cryptography;
using System.Text;
using Application.Features.Billing.Abstractions;
using Application.Features.Billing.Webhooks;
using Infrastructure.Payments.Stripe;
using Microsoft.Extensions.Options;

namespace Infrastructure.IntegrationTests.Billing;

public sealed class StripePaymentWebhookAuthenticatorTests
{
    private const string CurrentSecret = "whsec_current_test_only";
    private const string NextSecret = "whsec_next_test_only";

    [Theory]
    [InlineData(CurrentSecret)]
    [InlineData(NextSecret)]
    public void Authenticate_CurrentOrNextSecret_NormalizesAllowedEvent(string secret)
    {
        var body = EventJson("checkout.session.completed", StripeOptions.ApiVersion);
        var outcome = CreateAuthenticator().Authenticate(
            Encoding.UTF8.GetBytes(body),
            Signature(body, secret));

        Assert.Equal(PaymentWebhookAuthenticationStatus.Authenticated, outcome.Status);
        Assert.Equal(PaymentEventKind.CheckoutCompleted, outcome.PaymentEvent!.Kind);
        Assert.Equal("cs_test", outcome.PaymentEvent.ProviderCheckoutSessionId);
    }

    [Fact]
    public void Authenticate_ModifiedBody_FailsSignatureValidation()
    {
        var signed = EventJson("checkout.session.completed", StripeOptions.ApiVersion);
        var modified = signed.Replace("cs_test", "cs_other", StringComparison.Ordinal);

        var outcome = CreateAuthenticator().Authenticate(
            Encoding.UTF8.GetBytes(modified),
            Signature(signed, CurrentSecret));

        Assert.Equal(PaymentWebhookAuthenticationStatus.InvalidSignature, outcome.Status);
    }

    [Fact]
    public void Authenticate_IncompatibleApiVersion_FailsClosed()
    {
        var body = EventJson("checkout.session.completed", "2025-01-01.legacy");

        var outcome = CreateAuthenticator().Authenticate(
            Encoding.UTF8.GetBytes(body),
            Signature(body, CurrentSecret));

        Assert.Equal(PaymentWebhookAuthenticationStatus.IncompatibleApiVersion, outcome.Status);
    }

    [Fact]
    public void Authenticate_AuthenticatedEventOutsideAllowlist_IsIgnored()
    {
        var body = EventJson("customer.created", StripeOptions.ApiVersion);

        var outcome = CreateAuthenticator().Authenticate(
            Encoding.UTF8.GetBytes(body),
            Signature(body, CurrentSecret));

        Assert.Equal(PaymentWebhookAuthenticationStatus.AuthenticatedIgnored, outcome.Status);
        Assert.Null(outcome.PaymentEvent);
    }

    [Fact]
    public void Authenticate_SameEventId_ReusesStableCorrelationId()
    {
        var body = EventJson("checkout.session.completed", StripeOptions.ApiVersion);
        var authenticator = CreateAuthenticator();
        var first = authenticator.Authenticate(Encoding.UTF8.GetBytes(body), Signature(body, CurrentSecret));
        var second = authenticator.Authenticate(Encoding.UTF8.GetBytes(body), Signature(body, CurrentSecret));

        Assert.Equal(first.PaymentEvent!.CorrelationId, second.PaymentEvent!.CorrelationId);
        Assert.NotEqual(Guid.Empty, first.PaymentEvent.CorrelationId);
    }

    private static StripePaymentWebhookAuthenticator CreateAuthenticator() => new(
        Options.Create(new StripeOptions
        {
            Enabled = true,
            CurrentWebhookSecret = CurrentSecret,
            NextWebhookSecret = NextSecret,
            SignatureTolerance = TimeSpan.FromMinutes(5)
        }));

    private static string EventJson(string type, string apiVersion) =>
        $$"""
        {
          "id": "evt_test",
          "object": "event",
          "api_version": "{{apiVersion}}",
          "created": {{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}},
          "type": "{{type}}",
          "data": {
            "object": {
              "id": "cs_test",
              "object": "checkout.session",
              "customer": "cus_test",
              "subscription": "sub_test"
            }
          }
        }
        """;

    private static string Signature(string body, string secret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{timestamp}.{body}";
        var digest = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(signedPayload));
        return $"t={timestamp},v1={Convert.ToHexStringLower(digest)}";
    }
}
