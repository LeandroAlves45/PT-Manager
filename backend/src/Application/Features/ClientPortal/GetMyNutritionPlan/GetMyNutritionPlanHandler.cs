using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.ClientPortal.Abstractions;
using Application.Features.ClientPortal.Dtos;
using Application.Results;

namespace Application.Features.ClientPortal.GetMyNutritionPlan;

/// <summary>
/// Devolve o plano alimentar que o cliente escolheu seguir. Sem escolha válida, distingue "há
/// planos para escolher" de "não há nenhum plano": a app nunca escolhe por ele, mesmo que só
/// haja um. Cliente inexistente ou arquivado cai no mesmo NotFound de "sem plano".
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

        var plan = await _queries.GetSelectedAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            cancellationToken);
        if (plan is not null)
            return Result<MyNutritionPlanDto>.Success(plan);

        var available = await _queries.ListActiveAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            cancellationToken);

        return Result<MyNutritionPlanDto>.Failure(available.Count > 0
            ? ClientPortalErrors.NutritionPlanNotSelected
            : ClientPortalErrors.NutritionPlanNotAvailable);
    }
}
