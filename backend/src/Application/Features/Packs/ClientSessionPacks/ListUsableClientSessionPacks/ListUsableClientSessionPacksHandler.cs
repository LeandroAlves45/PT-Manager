using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Packs.ClientSessionPacks.Abstractions;
using Application.Features.Packs.ClientSessionPacks.Dtos;
using Application.Results;

namespace Application.Features.Packs.ClientSessionPacks.ListUsableClientSessionPacks;

/// <summary>Lista packs que ainda têm saldo para seleção explícita.</summary>
public sealed class ListUsableClientSessionPacksHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClientSessionPackQueries _queries;

    public ListUsableClientSessionPacksHandler(
        ITenantContext tenantContext,
        IClientSessionPackQueries queries
    )
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<IReadOnlyList<ClientSessionPackDto>>> HandleAsync(
        ListUsableClientSessionPacksQuery query,
        CancellationToken cancellationToken
    )
    {
        var actor = ActorAuthorization.RequireTrainer(_tenantContext, PackErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<IReadOnlyList<ClientSessionPackDto>>.Failure(actor.Error!);

        var packs = await _queries.ListUsableAsync(
            actor.Value.TrainerId,
            query.ClientId,
            cancellationToken
        );

        return Result<IReadOnlyList<ClientSessionPackDto>>.Success(packs);
    }
}
