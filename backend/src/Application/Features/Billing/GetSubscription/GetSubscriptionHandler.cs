using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Billing.Abstractions;
using Application.Features.Billing.Dtos;
using Application.Results;

namespace Application.Features.Billing.GetSubscription;

/// <summary>Consulta a subscription do personal trainer autenticado.</summary>
public sealed class GetSubscriptionHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly ISubscriptionQueryStore _store;

    public GetSubscriptionHandler(
        ITenantContext tenantContext,
        ISubscriptionQueryStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<SubscriptionDto>> HandleAsync(
        CancellationToken cancellationToken)
    {
        var actor = ActorAuthorization.RequireTrainer(
            _tenantContext,
            BillingErrors.TrainerOnly
        );
        if (!actor.IsSuccess)
            return Result<SubscriptionDto>.Failure(actor.Error!);

        var value = await _store.GetSubscriptionAsync(
            actor.Value.TrainerId,
            cancellationToken
        );

        return value is null
            ? Result<SubscriptionDto>.Failure(BillingErrors.SubscriptionNotFound)
            : Result<SubscriptionDto>.Success(value);
    }
}
