using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Results;

namespace Application.Features.Supplements.GetGlobalSupplement;

/// <summary>Obtém exclusivamente um suplemento global.</summary>
public sealed class GetGlobalSupplementHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IGlobalSupplementQueries _queries;

    public GetGlobalSupplementHandler(
        ITenantContext tenantContext,
        IGlobalSupplementQueries queries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<GlobalSupplementDto>> HandleAsync(
        GetGlobalSupplementQuery query,
        CancellationToken cancellationToken)
    {
        if (query.SupplementId == Guid.Empty)
            return Result<GlobalSupplementDto>.Failure(SupplementErrors.SupplementIdRequired);

        var actor = SupplementActorAuthorization.RequireAdministrator(_tenantContext);
        if (!actor.IsSuccess)
            return Result<GlobalSupplementDto>.Failure(actor.Error!);

        var supplement = await _queries.GetAsync(query.SupplementId, cancellationToken);

        return supplement is null
            ? Result<GlobalSupplementDto>.Failure(SupplementErrors.SupplementNotFound)
            : Result<GlobalSupplementDto>.Success(supplement);
    }
}
