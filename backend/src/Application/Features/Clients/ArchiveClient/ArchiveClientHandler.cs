using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Clients.Abstractions;
using Application.Results;

namespace Application.Features.Clients.ArchiveClient;

/// <summary>Arquiva reversivelmente um cliente e decrementa o contador uma única vez.</summary>
public sealed class ArchiveClientHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IClientStore _clientStore;

    /// <summary>Inicializa o caso de uso de arquivo.</summary>
    public ArchiveClientHandler(
        ITenantContext tenantContext,
        IClock clock,
        IClientStore clientStore)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _clientStore = clientStore ?? throw new ArgumentNullException(nameof(clientStore));
    }

    /// <summary>É idempotente quando o cliente já está arquivado.</summary>
    public async Task<Result> HandleAsync(
        ArchiveClientCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.ClientId == Guid.Empty)
            return Result.Failure(ClientErrors.ClientIdRequired());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, ClientErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        var outcome = await _clientStore.ArchiveAsync(
            command.ClientId,
            actor.Value.TrainerId,
            _clock.UtcNow,
            cancellationToken);

        return outcome switch
        {
            ArchiveClientStoreOutcome.Archived => Result.Success(),
            ArchiveClientStoreOutcome.AlreadyArchived => Result.Success(),
            ArchiveClientStoreOutcome.NotFound => Result.Failure(ClientErrors.ClientNotFound),
            ArchiveClientStoreOutcome.SubscriptionMissing =>
                throw new InvalidOperationException("Subscription slot missing for archiving client."),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };
    }
}


