using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.ClientPortal.Abstractions;
using Application.Features.ClientPortal.Dtos;
using Application.Results;

namespace Application.Features.ClientPortal.ListMyMealPlans;

/// <summary>
/// Lista os planos alimentares ativos do cliente autenticado, para ele escolher qual seguir.
/// Uma lista vazia é uma resposta válida: o personal trainer ainda não deu nenhum plano.
/// </summary>
public sealed class ListMyMealPlansHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IMyNutritionPlanQueries _queries;

    public ListMyMealPlansHandler(ITenantContext tenantContext, IMyNutritionPlanQueries queries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<IReadOnlyList<MyMealPlanSummaryDto>>> HandleAsync(
        CancellationToken cancellationToken)
    {
        var actor = ActorAuthorization.RequireClient(_tenantContext, ClientPortalErrors.ClientOnly);
        if (!actor.IsSuccess)
            return Result<IReadOnlyList<MyMealPlanSummaryDto>>.Failure(actor.Error!);

        var plans = await _queries.ListActiveAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            cancellationToken);

        return Result<IReadOnlyList<MyMealPlanSummaryDto>>.Success(plans);
    }
}
