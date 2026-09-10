namespace Infrastructure.Payments.Stripe;

/// <summary>Configuração validada da integração Stripe.</summary>
public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    public const string ApiVersion = "2026-08-26.dahlia";
    public bool Enabled { get; init; }
    public string? SecretKey { get; init; }
    public string? CurrentWebhookSecret { get; init; }
    public string? NextWebhookSecret { get; init; }
    public string? StarterPriceId { get; init; }
    public string? ProPriceId { get; init; }
    public Uri? CheckoutSuccessUrl { get; init; }
    public Uri? CheckoutCancelUrl { get; init; }
    public Uri? PortalReturnUrl { get; init; }
    public Uri? BillingManagementUrl { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public int MaxNetworkRetries { get; init; } = 2;
    public TimeSpan SignatureTolerance { get; init; } = TimeSpan.FromMinutes(5);
    public int MaximumWebhookBodySize { get; init; } = 262_144;
    public TimeSpan CheckoutSessionLifetime { get; init; } = TimeSpan.FromMinutes(30);
}
