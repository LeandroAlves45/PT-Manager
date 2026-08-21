using Application.Common.Abstractions;
using Application.Common.Authorization;
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
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<ClientSessionPackDto>> HandleAsync(
        GetClientSessionPackQuery query,
        CancellationToken cancellationToken
    )
    {
        var actor = ActorAuthorization.RequireTrainer(_tenantContext, PackErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<ClientSessionPackDto>.Failure(actor.Error!);

        var pack = await _queries.GetAsync(
            actor.Value.TrainerId,
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
