using Application.Features.Billing.Abstractions;
using Microsoft.Extensions.Options;
using Stripe;

namespace Infrastructure.Payments.Stripe;

/// <summary>
/// Cancela no Stripe, na hora e sem reembolso, a subscrição de quem passa a isento (U42).
/// Lê primeiro o estado para que repetir o pedido depois de um cancelamento seja sucesso.
/// </summary>
internal sealed class StripeSubscriptionCancellationGateway : ISubscriptionCancellationGateway
{
    private readonly StripeClientFactory _clientFactory;
    private readonly StripeOptions _options;

    public StripeSubscriptionCancellationGateway(
        StripeClientFactory clientFactory,
        IOptions<StripeOptions> options)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<BillingGatewayStatus> CancelImmediatelyAsync(
        string providerSubscriptionId,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return BillingGatewayStatus.Disabled;

        try
        {
            var service = new SubscriptionService(_clientFactory.GetRequiredClient());
            var subscription = await service.GetAsync(
                providerSubscriptionId,
                cancellationToken: cancellationToken);
            if (subscription.Status is "canceled" or "incomplete_expired")
                return BillingGatewayStatus.Success;

            await service.CancelAsync(
                providerSubscriptionId,
                new SubscriptionCancelOptions { Prorate = false, InvoiceNow = false },
                cancellationToken: cancellationToken);
            return BillingGatewayStatus.Success;
        }
        catch (StripeException exception)
        {
            var status = StripeFailureMapper.Map(exception);
            return status == BillingGatewayStatus.NotFound ? BillingGatewayStatus.Success : status;
        }
    }
}
