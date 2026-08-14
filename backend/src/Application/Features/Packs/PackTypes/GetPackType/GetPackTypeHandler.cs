using Application.Common.Abstractions;
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
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(queries);
        _tenantContext = tenantContext;
        _queries = queries;
    }

    public async Task<Result<PackTypeDto>> HandleAsync(
        GetPackTypeQuery query,
        CancellationToken cancellationToken
    )
    {
        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<PackTypeDto>.Failure(tenant.Error!);

        var packType = await _queries.GetAsync(
            tenant.Value,
            query.PackTypeId,
            cancellationToken
        );

        return packType is null
            ? Result<PackTypeDto>.Failure(PackErrors.PackTypeNotFound)
            : Result<PackTypeDto>.Success(packType);
    }
}
