using Application.Common.Abstractions;
using Application.Features.Training.TrainingPlans.Abstractions;
using Application.Features.Training.TrainingPlans.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.TrainingPlans.UpdateTrainingPlanStructure;

/// <summary>Reconcilia a árvore final dentro de uma transação.</summary>
public sealed class UpdateTrainingPlanStructureHandler
{
    private readonly IValidator<UpdateTrainingPlanStructureCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainingPlanStore _store;
    private readonly ITrainingPlanQueries _queries;

    public UpdateTrainingPlanStructureHandler(
        IValidator<UpdateTrainingPlanStructureCommand> validator,
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
        UpdateTrainingPlanStructureCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<TrainingPlanDetailsDto>.Failure(validation.ToApplicationError());

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<TrainingPlanDetailsDto>.Failure(tenant.Error!);

        var outcome = await _store.UpdateStructureAsync(
            tenant.Value,
            new UpdateTrainingPlanStructureWriteModel(
                command.TrainingPlanId,
                command.Structure),
            _clock.UtcNow,
            cancellationToken);

        if (outcome.Kind == TrainingPlanStoreResult.Status.Updated)
        {
            var details = await _queries.GetDetailsAsync(
                command.TrainingPlanId,
                cancellationToken);
            return Result<TrainingPlanDetailsDto>.Success(details
                ?? throw new InvalidOperationException(
                    "A committed TrainingPlan must be readable by its owner."));
        }

        return outcome.Kind switch
        {
            TrainingPlanStoreResult.Status.NotFound =>
                Result<TrainingPlanDetailsDto>.Failure(TrainingErrors.TrainingPlanNotFound),
            TrainingPlanStoreResult.Status.Inactive =>
                Result<TrainingPlanDetailsDto>.Failure(TrainingErrors.TrainingPlanInactive),
            TrainingPlanStoreResult.Status.StructureReferenceNotFound =>
                Result<TrainingPlanDetailsDto>.Failure(TrainingErrors.StructureReferenceNotFound),
            TrainingPlanStoreResult.Status.ExerciseReferenceNotFound =>
                Result<TrainingPlanDetailsDto>.Failure(TrainingErrors.ExerciseReferenceNotFound),
            TrainingPlanStoreResult.Status.ExerciseReferenceInactive =>
                Result<TrainingPlanDetailsDto>.Failure(TrainingErrors.ExerciseReferenceInactive),
            TrainingPlanStoreResult.Status.StructureHasHistory =>
                Result<TrainingPlanDetailsDto>.Failure(TrainingErrors.StructureHasHistory),
            TrainingPlanStoreResult.Status.StructureReorderRequiresFreeSlot =>
                Result<TrainingPlanDetailsDto>.Failure(TrainingErrors.StructureReorderRequiresFreeSlot),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
    }
}
