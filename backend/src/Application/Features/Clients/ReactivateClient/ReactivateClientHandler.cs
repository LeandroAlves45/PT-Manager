using Application.Common.Abstractions;
using Application.Errors;
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
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(clientStore);

        _tenantContext = tenantContext;
        _clock = clock;
        _clientStore = clientStore;
    }

    /// <summary>É idempotente quando o cliente já está ativo.</summary>
    public async Task<Result> HandleAsync(
        ReactivateClientCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.ClientId == Guid.Empty)
            return Result.Failure(Error.Validation(new List<ValidationError>
            {
                new ValidationError(
                    Field: "ClientId",
                    Code: "client_id_required",
                    Message: "Client ID is required."
                )
            }));

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (tenant.IsFailure)
            return Result.Failure(tenant.Error!);

        var outcome = await _clientStore.ReactivateAsync(
            command.ClientId,
            tenant.Value,
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

