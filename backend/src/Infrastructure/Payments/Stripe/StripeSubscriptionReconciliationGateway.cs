using Application.Common.Abstractions;
using Application.Features.Billing.Abstractions;
using Domain.ValueObjects;
using Microsoft.Extensions.Options;
using Stripe;

namespace Infrastructure.Payments.Stripe;

/// <summary>
/// Reconcilia o estado atual e converte todas as falhas para outcomes provider-neutral.
/// </summary>
internal sealed class StripeSubscriptionReconciliationGateway : ISubscriptionReconciliationGateway
{
    private readonly StripeClientFactory _clientFactory;
    private readonly StripeOptions _options;
    private readonly IClock _clock;

    public StripeSubscriptionReconciliationGateway(
        StripeClientFactory clientFactory,
        IOptions<StripeOptions> options,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(options);

        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<SubscriptionReconciliationOutcome> GetSubscriptionSnapshotAsync(
        string? providerCustomerId,
        string? providerSubscriptionId,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return new(SubscriptionReconciliationStatus.Disabled);

        try
        {
            var service = new SubscriptionService(_clientFactory.GetRequiredClient());
            var subscription = await ResolveSubscriptionAsync(
                service,
                providerCustomerId,
                providerSubscriptionId,
                cancellationToken);

            if (subscription.Status != SubscriptionReconciliationStatus.Success)
                return subscription;

            var value = subscription.Snapshot!;
            if (string.IsNullOrWhiteSpace(value.ProviderCustomerId) ||
                string.IsNullOrWhiteSpace(value.ProviderSubscriptionId) ||
                string.IsNullOrWhiteSpace(value.ProviderStatus))
                return new(SubscriptionReconciliationStatus.InvalidResponse);

            return subscription;
        }
        catch (StripeException exception)
        {
            return new(Map(exception));
        }
    }

    private async Task<SubscriptionReconciliationOutcome> ResolveSubscriptionAsync(
        SubscriptionService service,
        string? providerCustomerId,
        string? providerSubscriptionId,
        CancellationToken cancellationToken)
    {
        Subscription value;
        if (!string.IsNullOrWhiteSpace(providerSubscriptionId))
        {
            value = await service.GetAsync(
                providerSubscriptionId,
                cancellationToken: cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(providerCustomerId))
        {
            var list = await service.ListAsync(
                new SubscriptionListOptions
                {
                    Customer = providerCustomerId,
                    Status = "all",
                    Limit = 2
                },
                cancellationToken: cancellationToken);

            if (list.Data.Count == 0)
                return new(SubscriptionReconciliationStatus.NotFound);
            if (list.Data.Count != 1)
                return new(SubscriptionReconciliationStatus.ConfigurationMismatch);
            value = list.Data[0];
        }
        else
        {
            return new(SubscriptionReconciliationStatus.NotFound);
        }

        if (string.IsNullOrWhiteSpace(value.CustomerId) || value.Items.Data.Count != 1)
            return new(SubscriptionReconciliationStatus.ConfigurationMismatch);

        var priceId = value.Items.Data[0].Price?.Id;
        var tier = priceId switch
        {
            var id when id == _options.StarterPriceId => SubscriptionTier.Starter,
            var id when id == _options.ProPriceId => SubscriptionTier.Pro,
            _ => null
        };
        if (tier is null)
            return new(SubscriptionReconciliationStatus.ConfigurationMismatch);

        // ObservedAt representa o instante em que esta leitura autoritativa terminou.
        // O event.created não é usado, pois a entrega de webhooks pode chegar fora de ordem.
        var snapshot = new ProviderSubscriptionSnapshot(
            value.CustomerId,
            value.Id,
            tier,
            value.Status,
            value.TrialEnd,
            _clock.UtcNow);

        return new(SubscriptionReconciliationStatus.Success, snapshot);
    }

    private static SubscriptionReconciliationStatus Map(StripeException exception) =>
        StripeFailureMapper.Map(exception) switch
        {
            BillingGatewayStatus.TransientFailure => SubscriptionReconciliationStatus.TransientFailure,
            BillingGatewayStatus.NotFound => SubscriptionReconciliationStatus.NotFound,
            _ => SubscriptionReconciliationStatus.InvalidResponse
        };
}
