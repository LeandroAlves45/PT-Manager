using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Training.TrainingPlans.Abstractions;
using Application.Features.Training.TrainingPlans.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.TrainingPlans.UpdateTrainingPlanMetadata;

/// <summary>Atualiza metadados e protege datas de planos com histórico.</summary>
public sealed class UpdateTrainingPlanMetadataHandler
{
    private readonly IValidator<UpdateTrainingPlanMetadataCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainingPlanStore _store;
    private readonly ITrainingPlanQueries _queries;

    public UpdateTrainingPlanMetadataHandler(
        IValidator<UpdateTrainingPlanMetadataCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        ITrainingPlanStore store,
        ITrainingPlanQueries queries)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<TrainingPlanDetailsDto>> HandleAsync(
        UpdateTrainingPlanMetadataCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<TrainingPlanDetailsDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, TrainingErrors.TrainingPlanTrainerOnly);
        if (!actor.IsSuccess)
            return Result<TrainingPlanDetailsDto>.Failure(actor.Error!);

        var outcome = await _store.UpdateMetadataAsync(
            actor.Value.TrainerId,
            new UpdateTrainingPlanMetadataWriteModel(
                command.TrainingPlanId,
                command.Name,
                command.Description,
                command.TrainingModality,
                command.Notes,
                command.StartDate,
                command.EndDate),
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

        return outcome.ToDetailsFailure();
    }
}
