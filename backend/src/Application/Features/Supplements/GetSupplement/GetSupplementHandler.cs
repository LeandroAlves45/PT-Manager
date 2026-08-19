using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Results;

namespace Application.Features.Supplements.GetSupplement;

/// <summary>Obtém um suplemento global ativo ou privado do tenant.</summary>
public sealed class GetSupplementHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly ISupplementQueries _queries;

    public GetSupplementHandler(
        ITenantContext tenantContext,
        ISupplementQueries queries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<SupplementDto>> HandleAsync(
        GetSupplementQuery query,
        CancellationToken cancellationToken)
    {
        if (query.SupplementId == Guid.Empty)
            return Result<SupplementDto>.Failure(SupplementErrors.SupplementIdRequired);

        var actor = SupplementActorAuthorization.RequireTrainer(_tenantContext);
        if (!actor.IsSuccess)
            return Result<SupplementDto>.Failure(actor.Error!);

        var supplement = await _queries.GetAsync(
            actor.Value.TrainerId,
            query.SupplementId,
            cancellationToken);

        return supplement is null
            ? Result<SupplementDto>.Failure(SupplementErrors.SupplementNotFound)
            : Result<SupplementDto>.Success(supplement);
    }
}
