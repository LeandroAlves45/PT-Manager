using Application.Common.Abstractions;
using Application.Features.Training.TrainingPlans.Abstractions;
using Application.Features.Training.TrainingPlans.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.TrainingPlans.CreateTrainingPlan;

/// <summary>Cria um plano de treino ativo com a árvore completa.</summary>
public sealed class CreateTrainingPlanHandler
{
    private readonly IValidator<CreateTrainingPlanCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainingPlanStore _store;
    private readonly ITrainingPlanQueries _queries;

    public CreateTrainingPlanHandler(
        IValidator<CreateTrainingPlanCommand> validator,
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
        CreateTrainingPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<TrainingPlanDetailsDto>.Failure(validation.ToApplicationError());

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<TrainingPlanDetailsDto>.Failure(tenant.Error!);

        var model = new CreateTrainingPlanWriteModel(
            command.ClientId,
            command.Name,
            command.Description,
            command.TrainingModality,
            command.Notes,
            command.StartDate,
            command.EndDate,
            command.Structure);

        var outcome = await _store.CreateAsync(
            tenant.Value,
            model,
            _clock.UtcNow,
            cancellationToken);

        if (outcome.Kind == TrainingPlanStoreResult.Status.Created)
        {
            var details = await _queries.GetDetailsAsync(
                outcome.TrainingPlanId!.Value,
                cancellationToken);
            return Result<TrainingPlanDetailsDto>.Success(details
                ?? throw new InvalidOperationException(
                    "A committed TrainingPlan must be readable by its owner."));
        }

        return outcome.Kind switch
        {
            TrainingPlanStoreResult.Status.ClientNotFound =>
                Result<TrainingPlanDetailsDto>.Failure(TrainingErrors.ClientNotFound),
            TrainingPlanStoreResult.Status.ExerciseReferenceNotFound =>
                Result<TrainingPlanDetailsDto>.Failure(TrainingErrors.ExerciseReferenceNotFound),
            TrainingPlanStoreResult.Status.ExerciseReferenceInactive =>
                Result<TrainingPlanDetailsDto>.Failure(TrainingErrors.ExerciseReferenceInactive),
            TrainingPlanStoreResult.Status.ActivePlanConflict =>
                Result<TrainingPlanDetailsDto>.Failure(TrainingErrors.ActiveTrainingPlanConflict),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
    }
}
