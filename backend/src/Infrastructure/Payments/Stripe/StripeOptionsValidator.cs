using Microsoft.Extensions.Options;

namespace Infrastructure.Payments.Stripe;

/// <summary>Falha no arranque quando billing ativo não tem configuração segura.</summary>
internal sealed class StripeOptionsValidator : IValidateOptions<StripeOptions>
{
    public ValidateOptionsResult Validate(string? name, StripeOptions options)
    {
        var failures = new List<string>();
        if (options.Timeout <= TimeSpan.Zero || options.Timeout > TimeSpan.FromMinutes(2))
            failures.Add("Stripe timeout is outside the allowed range.");

        if (options.MaxNetworkRetries is < 0 or > 3)
            failures.Add("Stripe network retries are outside the allowed range.");

        if (options.SignatureTolerance <= TimeSpan.Zero ||
            options.SignatureTolerance > TimeSpan.FromMinutes(5))
            failures.Add("Stripe signature tolerance is outside the allowed range.");

        if (options.MaximumWebhookBodySize is < 1024 or > 1_048_576)
            failures.Add("Stripe webhook body limit is outside the allowed range.");

        if (options.CheckoutSessionLifetime < TimeSpan.FromMinutes(30) ||
            options.CheckoutSessionLifetime > TimeSpan.FromHours(24))
            failures.Add("Stripe checkout lifetime is outside the allowed range.");

        if (options.Enabled)
        {
            Require(options.SecretKey, "Stripe secret key is required.", failures);
            Require(options.CurrentWebhookSecret, "Stripe current webhook secret is required.", failures);
            Require(options.StarterPriceId, "Stripe STARTER price ID is required.", failures);
            Require(options.ProPriceId, "Stripe PRO price ID is required.", failures);
            if (options.StarterPriceId == options.ProPriceId)
                failures.Add("Stripe Price IDs must be different.");

            ValidateUri(options.CheckoutSuccessUrl, "Checkout success URL.", failures);
            ValidateUri(options.CheckoutCancelUrl, "Checkout cancel URL.", failures);
            ValidateUri(options.PortalReturnUrl, "Portal return URL.", failures);
            ValidateUri(options.BillingManagementUrl, "Billing management URL.", failures);
            ValidateCanonicalHosts(options, failures);
        }
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void Require(string? value, string error, ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
            failures.Add(error);
    }

    private static void ValidateUri(Uri? uri, string name, ICollection<string> failures)
    {
        if (uri is null || !uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
            failures.Add($"{name} must be an absolute HTTPS URL without user-info or fragment.");
    }

    private static void ValidateCanonicalHosts(
        StripeOptions options,
        ICollection<string> failures)
    {
        var hosts = new[]
        {
            options.CheckoutSuccessUrl?.IdnHost,
            options.CheckoutCancelUrl?.IdnHost,
            options.PortalReturnUrl?.IdnHost,
            options.BillingManagementUrl?.IdnHost
        };

        // As quatro rotas pertencem ao frontend canónico configurado pelo backend.
        // Um host divergente normalmente indica configuração de redirect incorreta.
        if (hosts.All(host => host is not null) &&
            hosts.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1)
            failures.Add("Stripe redirect URLs must use the same canonical host.");
    }
}
