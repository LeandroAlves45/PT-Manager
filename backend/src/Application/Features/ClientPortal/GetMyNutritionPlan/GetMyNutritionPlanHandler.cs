using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.ClientPortal.Abstractions;
using Application.Features.ClientPortal.Dtos;
using Application.Results;

namespace Application.Features.ClientPortal.GetMyNutritionPlan;

/// <summary>
/// Devolve o plano alimentar ativo do cliente autenticado. Cliente inexiste,
/// arquivado ou sem plano devolvem o mesmo NotFound, sem revelar qual condição falhou.
/// </summary>
public sealed class GetMyNutritionPlanHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IMyNutritionPlanQueries _queries;

    public GetMyNutritionPlanHandler(
        ITenantContext tenantContext,
        IMyNutritionPlanQueries queries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<MyNutritionPlanDto>> HandleAsync(
        CancellationToken cancellationToken)
    {
        var actor = ActorAuthorization.RequireClient(
            _tenantContext,
            ClientPortalErrors.ClientOnly);
        if (!actor.IsSuccess)
            return Result<MyNutritionPlanDto>.Failure(actor.Error!);

        var plan = await _queries.GetActiveAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            cancellationToken);

        return plan is null
            ? Result<MyNutritionPlanDto>.Failure(ClientPortalErrors.NutritionPlanNotAvailable)
            : Result<MyNutritionPlanDto>.Success(plan);
    }
}
