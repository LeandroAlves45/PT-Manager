using Application.Common.Abstractions;
using Application.Features.Assessments.InitialAssessments.Abstractions;
using Application.Features.Assessments.InitialAssessments.Dtos;
using Application.Results;

namespace Application.Features.Assessments.InitialAssessments.MarkInitialAssessmentReviewed;

/// <summary>Avaliação inicial que o personal trainer deu por revista.</summary>
public sealed record MarkInitialAssessmentReviewedCommand(Guid AssessmentId);

/// <summary>
/// Marca como revista a avaliação inicial que o cliente gravou no portal; ela sai da lista
/// «por rever». Idempotente: repetir devolve o instante original. Marcar como revista não é
/// condição para criar planos.
/// </summary>
public sealed class MarkInitialAssessmentReviewedHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IInitialAssessmentStore _store;

    public MarkInitialAssessmentReviewedHandler(
        ITenantContext tenantContext,
        IClock clock,
        IInitialAssessmentStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<InitialAssessmentDto>> HandleAsync(
        MarkInitialAssessmentReviewedCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.AssessmentId == Guid.Empty)
            return Result<InitialAssessmentDto>.Failure(AssessmentErrors.InitialAssessmentNotFound);

        var tenant = AssessmentActorAuthorization.RequireTrainer(_tenantContext);
        if (!tenant.IsSuccess)
            return Result<InitialAssessmentDto>.Failure(tenant.Error!);

        var outcome = await _store.MarkReviewedAsync(
            tenant.Value,
            command.AssessmentId,
            _clock.UtcNow,
            cancellationToken);

        return outcome.ToResult();
    }
}
