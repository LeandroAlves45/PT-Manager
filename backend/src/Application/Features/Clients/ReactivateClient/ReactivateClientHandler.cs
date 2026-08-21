using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Clients.Abstractions;
using Application.Results;

namespace Application.Features.Clients.ReactivateClient;

/// <summary>Reativa um cliente arquivado depois de reservar capacidade na subscrição.</summary>
public sealed class ReactivateClientHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IClientStore _clientStore;

    /// <summary>Inicializa o caso de uso de reativação.</summary>
    public ReactivateClientHandler(
        ITenantContext tenantContext,
        IClock clock,
        IClientStore clientStore)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _clientStore = clientStore ?? throw new ArgumentNullException(nameof(clientStore));
    }

    /// <summary>É idempotente quando o cliente já está ativo.</summary>
    public async Task<Result> HandleAsync(
        ReactivateClientCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.ClientId == Guid.Empty)
            return Result.Failure(ClientErrors.ClientIdRequired());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, ClientErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        var outcome = await _clientStore.ReactivateAsync(
            command.ClientId,
            actor.Value.TrainerId,
            _clock.UtcNow,
            cancellationToken);

        return outcome switch
        {
            ReactivateClientStoreOutcome.Reactivated => Result.Success(),
            ReactivateClientStoreOutcome.AlreadyActive => Result.Success(),
            ReactivateClientStoreOutcome.NotFound => Result.Failure(ClientErrors.ClientNotFound),
            ReactivateClientStoreOutcome.SubscriptionInactive =>
                Result.Failure(ClientErrors.SubscriptionInactive),
            ReactivateClientStoreOutcome.SubscriptionSuspended =>
                Result.Failure(ClientErrors.SubscriptionSuspended),
            ReactivateClientStoreOutcome.SubscriptionCancelled =>
                Result.Failure(ClientErrors.SubscriptionCancelled),
            ReactivateClientStoreOutcome.ClientLimitReached =>
                Result.Failure(ClientErrors.ClientLimitReached),
            ReactivateClientStoreOutcome.SubscriptionMissing =>
                throw new InvalidOperationException("Subscription slot missing for reactivating client."),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };
    }
}

