using Infrastructure.Payments.Stripe;
using Microsoft.Extensions.Options;

namespace Infrastructure.IntegrationTests.Billing;

public sealed class StripeOptionsTests
{
    [Fact]
    public void Validate_DisabledWithoutSecrets_Succeeds()
    {
        var result = new StripeOptionsValidator().Validate(null, new StripeOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_EnabledWithCanonicalConfiguration_Succeeds()
    {
        var result = new StripeOptionsValidator().Validate(null, EnabledOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_EnabledWithDifferentRedirectHost_Fails()
    {
        var options = EnabledOptions(useDifferentPortalHost: true);

        var result = new StripeOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure =>
            failure.Contains("same canonical host", StringComparison.Ordinal));
    }

    [Fact]
    public void ClientFactory_DisabledWithoutSecret_DoesNotCreateClient()
    {
        using var factory = new StripeClientFactory(Options.Create(new StripeOptions()));

        Assert.Throws<InvalidOperationException>(() => factory.GetRequiredClient());
    }

    private static StripeOptions EnabledOptions(bool useDifferentPortalHost = false) => new()
    {
        Enabled = true,
        SecretKey = "sk_test_documentation_only",
        CurrentWebhookSecret = "whsec_documentation_only",
        StarterPriceId = "price_starter",
        ProPriceId = "price_pro",
        CheckoutSuccessUrl = new Uri("https://app.example.test/billing/success"),
        CheckoutCancelUrl = new Uri("https://app.example.test/billing/cancel"),
        PortalReturnUrl = new Uri(useDifferentPortalHost
            ? "https://other.example.test/settings/billing"
            : "https://app.example.test/settings/billing"),
        BillingManagementUrl = new Uri("https://app.example.test/settings/billing")
    };
}
