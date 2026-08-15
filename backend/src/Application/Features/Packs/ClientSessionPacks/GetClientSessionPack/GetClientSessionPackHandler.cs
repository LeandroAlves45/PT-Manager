using Application.Common.Abstractions;
using Application.Features.Packs.ClientSessionPacks.Abstractions;
using Application.Features.Packs.ClientSessionPacks.Dtos;
using Application.Results;

namespace Application.Features.Packs.ClientSessionPacks.GetClientSessionPack;

/// <summary>Obtém um pack atribuído pertencente ao tenant.</summary>
public sealed class GetClientSessionPackHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClientSessionPackQueries _queries;

    public GetClientSessionPackHandler(
        ITenantContext tenantContext,
        IClientSessionPackQueries queries
    )
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(queries);

        _tenantContext = tenantContext;
        _queries = queries;
    }

    public async Task<Result<ClientSessionPackDto>> HandleAsync(
        GetClientSessionPackQuery query,
        CancellationToken cancellationToken
    )
    {
        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<ClientSessionPackDto>.Failure(tenant.Error!);

        var pack = await _queries.GetAsync(
            tenant.Value,
            query.ClientSessionPackId,
            cancellationToken
        );
        return pack is null
            ? Result<ClientSessionPackDto>.Failure(
                PackErrors.ClientSessionPackNotFound
            )
            : Result<ClientSessionPackDto>.Success(pack);
    }
}
