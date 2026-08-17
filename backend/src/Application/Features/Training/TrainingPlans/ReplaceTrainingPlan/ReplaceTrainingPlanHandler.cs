using Application.Common.Abstractions;
using Application.Features.Training.TrainingPlans.Abstractions;
using Application.Features.Training.TrainingPlans.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.TrainingPlans.ReplaceTrainingPlan;

/// <summary>Substitui um plano de treino por uma árvore integralmente nova.</summary>
public sealed class ReplaceTrainingPlanHandler
{
    private readonly IValidator<ReplaceTrainingPlanCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainingPlanStore _store;
    private readonly ITrainingPlanQueries _queries;

    public ReplaceTrainingPlanHandler(
        IValidator<ReplaceTrainingPlanCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        ITrainingPlanStore store,
        ITrainingPlanQueries queries)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(queries);

        _validator = validator;
        _tenantContext = tenantContext;
        _clock = clock;
        _store = store;
        _queries = queries;
    }

    public async Task<Result<TrainingPlanDetailsDto>> HandleAsync(
        ReplaceTrainingPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<TrainingPlanDetailsDto>.Failure(validation.ToApplicationError());

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<TrainingPlanDetailsDto>.Failure(tenant.Error!);

        var outcome = await _store.ReplaceAsync(
            tenant.Value,
            new ReplaceTrainingPlanWriteModel(
                command.TrainingPlanId,
                command.Name,
                command.Description,
                command.TrainingModality,
                command.Notes,
                command.StartDate,
                command.EndDate,
                command.Structure),
            _clock.UtcNow,
            cancellationToken);

        if (outcome.Kind == TrainingPlanStoreResult.Status.Replaced)
        {
            var details = await _queries.GetDetailsAsync(
                outcome.TrainingPlanId!.Value,
                cancellationToken);
            return Result<TrainingPlanDetailsDto>.Success(details
                ?? throw new InvalidOperationException(
                    "A committed replacement must be readable by its owner."));
        }

        return outcome.ToDetailsFailure();
    }
}
