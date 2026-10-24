using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Assessments.InitialAssessments.Abstractions;
using Application.Results;

namespace Application.Features.ClientPortal.GetMyInitialAssessment;

/// <summary>Pede o estado da avaliação inicial do cliente autenticado.</summary>
public sealed record GetMyInitialAssessmentStatusQuery;

/// <summary>Se a avaliação inicial ainda falta. Nunca traz os dados: o cliente não a volta a ver.</summary>
public sealed record MyInitialAssessmentStatusDto(bool Pending);

/// <summary>
/// Diz ao portal se o cliente ainda tem de preencher a avaliação inicial. Depois de gravada,
/// por ele ou pelo personal trainer, deixa de estar pendente.
/// </summary>
public sealed class GetMyInitialAssessmentStatusHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IInitialAssessmentQueries _queries;

    public GetMyInitialAssessmentStatusHandler(
        ITenantContext tenantContext,
        IInitialAssessmentQueries queries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<MyInitialAssessmentStatusDto>> HandleAsync(
        GetMyInitialAssessmentStatusQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var actor = ActorAuthorization.RequireClient(_tenantContext, ClientPortalErrors.ClientOnly);
        if (!actor.IsSuccess)
            return Result<MyInitialAssessmentStatusDto>.Failure(actor.Error!);

        var pending = await _queries.IsPendingForClientUserAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            cancellationToken);

        return pending is null
            ? Result<MyInitialAssessmentStatusDto>.Failure(ClientPortalErrors.ProfileNotAvailable)
            : Result<MyInitialAssessmentStatusDto>.Success(new MyInitialAssessmentStatusDto(pending.Value));
    }
}
