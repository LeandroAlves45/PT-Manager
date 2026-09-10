using Application.Features.Billing.Abstractions;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.BillingPortal;

namespace Infrastructure.Payments.Stripe;

/// <summary>Adapter Stripe para Customer Portal alojado.</summary>
internal sealed class StripeCustomerPortalGateway : ICustomerPortalGateway
{
    private readonly StripeClientFactory _clientFactory;
    private readonly StripeOptions _options;

    public StripeCustomerPortalGateway(
        StripeClientFactory clientFactory,
        IOptions<StripeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<CustomerPortalOutcome> CreateAsync(
        CreateCustomerPortalRequest request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return new(BillingGatewayStatus.Disabled);

        try
        {
            var session = await new SessionService(_clientFactory.GetRequiredClient()).CreateAsync(
                new SessionCreateOptions
                {
                    Customer = request.ProviderCustomerId,
                    ReturnUrl = _options.PortalReturnUrl!.AbsoluteUri
                },
                new RequestOptions { IdempotencyKey = request.IdempotencyKey },
                cancellationToken);

            return Uri.TryCreate(session.Url, UriKind.Absolute, out var url) &&
                url.Scheme == Uri.UriSchemeHttps
                ? new(BillingGatewayStatus.Success, url)
                : new(BillingGatewayStatus.InvalidResponse);
        }
        catch (StripeException exception)
        {
            return new(StripeFailureMapper.Map(exception));
        }
    }
}
