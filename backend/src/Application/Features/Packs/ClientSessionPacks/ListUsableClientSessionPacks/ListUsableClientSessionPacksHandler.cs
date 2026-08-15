using Application.Common.Abstractions;
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
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(queries);

        _tenantContext = tenantContext;
        _queries = queries;
    }

    public async Task<Result<IReadOnlyList<ClientSessionPackDto>>> HandleAsync(
        ListUsableClientSessionPacksQuery query,
        CancellationToken cancellationToken
    )
    {
        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<IReadOnlyList<ClientSessionPackDto>>.Failure(tenant.Error!);

        var packs = await _queries.ListUsableAsync(
            tenant.Value,
            query.ClientId,
            cancellationToken
        );

        return Result<IReadOnlyList<ClientSessionPackDto>>.Success(packs);
    }
}
