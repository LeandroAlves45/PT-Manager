using Application.Common.Abstractions;
using Application.Features.Training.ExerciseSetLogs.Abstractions;
using Application.Features.Training.ExerciseSetLogs.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.ExerciseSetLogs.RecordExerciseSetLog;

/// <summary>Regista uma executção num plano ativo.</summary>
public sealed class RecordExerciseSetLogHandler
{
    private readonly IValidator<RecordExerciseSetLogCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IExerciseSetLogStore _store;
    private readonly IExerciseSetLogQueries _queries;

    public RecordExerciseSetLogHandler(
        IValidator<RecordExerciseSetLogCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IExerciseSetLogStore store,
        IExerciseSetLogQueries queries)
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

    public async Task<Result<ClientExerciseSetLogDto>> HandleAsync(
        RecordExerciseSetLogCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<ClientExerciseSetLogDto>.Failure(validation.ToApplicationError());

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<ClientExerciseSetLogDto>.Failure(tenant.Error!);

        var now = DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc);
        var outcome = await _store.RecordAsync(
            tenant.Value,
            new RecordExerciseSetLogWriteModel(
                command.TrainingPlanDayExerciseId,
                command.SetNumber,
                command.WeightKg,
                command.RepsDone,
                command.Notes,
                command.PerformedAt),
            new DateTimeOffset(now),
            now,
            cancellationToken);

        if (outcome.Kind == ExerciseSetLogStoreResult.Status.Recorded)
        {
            var dto = await _queries.GetAsync(outcome.Log!.Id, cancellationToken);
            return Result<ClientExerciseSetLogDto>.Success(dto
                ?? throw new InvalidOperationException(
                    "A committed exercise set log must be readable."));
        }

        return outcome.Kind switch
        {
            ExerciseSetLogStoreResult.Status.NotFound =>
                Result<ClientExerciseSetLogDto>.Failure(TrainingErrors.StructureReferenceNotFound),
            ExerciseSetLogStoreResult.Status.TrainingPlanInactive =>
                Result<ClientExerciseSetLogDto>.Failure(TrainingErrors.TrainingPlanInactive),
            ExerciseSetLogStoreResult.Status.SetNotFound =>
                Result<ClientExerciseSetLogDto>.Failure(TrainingErrors.SetNotFound),
            ExerciseSetLogStoreResult.Status.PerformedAtInFuture =>
                Result<ClientExerciseSetLogDto>.Failure(TrainingErrors.PerformedAtInFuture()),
            ExerciseSetLogStoreResult.Status.PerformedAtOutsidePlan =>
                Result<ClientExerciseSetLogDto>.Failure(TrainingErrors.PerformedAtOutsidePlan()),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
    }
}
