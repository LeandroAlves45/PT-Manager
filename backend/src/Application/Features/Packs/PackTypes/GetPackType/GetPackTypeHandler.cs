using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Packs.PackTypes.Abstractions;
using Application.Features.Packs.PackTypes.Dtos;
using Application.Results;

namespace Application.Features.Packs.PackTypes.GetPackType;

/// <summary>Obtém um tipo de pack pertencente ao tenant.</summary>
public sealed class GetPackTypeHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IPackTypeQueries _queries;

    public GetPackTypeHandler(
        ITenantContext tenantContext,
        IPackTypeQueries queries
    )
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<PackTypeDto>> HandleAsync(
        GetPackTypeQuery query,
        CancellationToken cancellationToken
    )
    {
        var actor = ActorAuthorization.RequireTrainer(_tenantContext, PackErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<PackTypeDto>.Failure(actor.Error!);

        var packType = await _queries.GetAsync(
            actor.Value.TrainerId,
            query.PackTypeId,
            cancellationToken
        );

        return packType is null
            ? Result<PackTypeDto>.Failure(PackErrors.PackTypeNotFound)
            : Result<PackTypeDto>.Success(packType);
    }
}
