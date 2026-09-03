using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.ClientPortal.Abstractions;
using Application.Features.ClientPortal.Dtos;
using Application.Results;

namespace Application.Features.ClientPortal.GetMyTrainingPlan;

/// <summary>
/// Devolve o plano de treino ativo do cliente autenticado. Cliente inexiste,
/// arquivado ou sem plano devolvem o mesmo NotFound, sem revelar qual condição falhou.
/// </summary>
public sealed class GetMyTrainingPlanHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IMyTrainingPlanQueries _queries;

    public GetMyTrainingPlanHandler(
        ITenantContext tenantContext,
        IMyTrainingPlanQueries queries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<MyTrainingPlanDto>> HandleAsync(
        CancellationToken cancellationToken)
    {
        var actor = ActorAuthorization.RequireClient(
            _tenantContext,
            ClientPortalErrors.ClientOnly);
        if (!actor.IsSuccess)
            return Result<MyTrainingPlanDto>.Failure(actor.Error!);

        var plan = await _queries.GetActiveAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            cancellationToken);

        return plan is null
            ? Result<MyTrainingPlanDto>.Failure(ClientPortalErrors.TrainingPlanNotAvailable)
            : Result<MyTrainingPlanDto>.Success(plan);
    }
}
