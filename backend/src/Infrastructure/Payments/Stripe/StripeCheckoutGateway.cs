using Application.Features.Billing.Abstractions;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Infrastructure.Payments.Stripe;

/// <summary>Adapter Stripe para customer e Checkout alojados.</summary>
internal sealed class StripeCheckoutGateway : ICheckoutGateway
{
    private readonly StripeClientFactory _clientFactory;
    private readonly StripeOptions _options;

    public StripeCheckoutGateway(StripeClientFactory clientFactory, IOptions<StripeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<EnsureCustomerOutcome> EnsureCustomerAsync(
        EnsureCustomerRequest request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return new(BillingGatewayStatus.Disabled);

        try
        {
            var customer = await new CustomerService(_clientFactory.GetRequiredClient()).CreateAsync(
                new CustomerCreateOptions { Email = request.Email },
                new RequestOptions { IdempotencyKey = request.IdempotencyKey },
                cancellationToken);

            return string.IsNullOrWhiteSpace(customer.Id)
                ? new(BillingGatewayStatus.InvalidResponse)
                : new(BillingGatewayStatus.Success, customer.Id);
        }
        catch (StripeException exception)
        {
            return new(StripeFailureMapper.Map(exception));
        }
    }

    public async Task<CheckoutSessionOutcome> CreateSessionAsync(
        CreateCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return new(BillingGatewayStatus.Disabled);

        try
        {
            var priceId = request.Tier.Value == "STARTER"
                ? _options.StarterPriceId
                : _options.ProPriceId;

            var create = new SessionCreateOptions
            {
                Mode = "subscription",
                Customer = request.ProviderCustomerId,
                SuccessUrl = _options.CheckoutSuccessUrl!.AbsoluteUri,
                CancelUrl = _options.CheckoutCancelUrl!.AbsoluteUri,
                ExpiresAt = DateTime.UtcNow.Add(_options.CheckoutSessionLifetime),
                LineItems = [new SessionLineItemOptions { Price = priceId, Quantity = 1 }]
            };
            if (request.EffectiveTrialEndsAt.HasValue)
                create.SubscriptionData = new SessionSubscriptionDataOptions
                {
                    TrialEnd = request.EffectiveTrialEndsAt.Value
                };

            var session = await new SessionService(_clientFactory.GetRequiredClient()).CreateAsync(
                create,
                new RequestOptions { IdempotencyKey = request.IdempotencyKey },
                cancellationToken);

            return Validate(session);
        }
        catch (StripeException exception)
        {
            return new(StripeFailureMapper.Map(exception));
        }
    }

    public async Task<CheckoutSessionOutcome> GetSessionAsync(
        string providerSessionId,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return new(BillingGatewayStatus.Disabled);

        try
        {
            var session = await new SessionService(_clientFactory.GetRequiredClient()).GetAsync(
                providerSessionId,
                cancellationToken: cancellationToken);
            return Validate(session);
        }
        catch (StripeException exception)
        {
            return new(StripeFailureMapper.Map(exception));
        }
    }

    private static CheckoutSessionOutcome Validate(Session session)
    {
        if (string.IsNullOrWhiteSpace(session.Id) || string.IsNullOrWhiteSpace(session.Url) ||
            !Uri.TryCreate(session.Url, UriKind.Absolute, out var url) ||
            url.Scheme != Uri.UriSchemeHttps || session.ExpiresAt <= DateTime.UtcNow)
            return new(BillingGatewayStatus.InvalidResponse);

        return new(BillingGatewayStatus.Success, session.Id, url, session.ExpiresAt);
    }
}
