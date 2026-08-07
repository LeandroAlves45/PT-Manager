using Application.Common.Abstractions;
using Application.Errors;
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
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(clientStore);

        _tenantContext = tenantContext;
        _clock = clock;
        _clientStore = clientStore;
    }

    /// <summary>É idempotente quando o cliente já está arquivado.</summary>
    public async Task<Result> HandleAsync(
        ArchiveClientCommand command,
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

        var outcome = await _clientStore.ArchiveAsync(
            command.ClientId,
            tenant.Value,
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


