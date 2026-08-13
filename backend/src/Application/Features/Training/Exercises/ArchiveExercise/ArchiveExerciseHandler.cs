using Application.Common.Abstractions;
using Application.Errors;
using Application.Features.Training.Exercises.Abstractions;
using Application.Results;

namespace Application.Features.Training.Exercises.ArchiveExercise;

/// <summary>Arquiva um exercício privado de forma idempotente.</summary>
public sealed class ArchiveExerciseHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IExerciseStore _exerciseStore;

    public ArchiveExerciseHandler(
        ITenantContext tenantContext,
        IClock clock,
        IExerciseStore exerciseStore
    )
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(exerciseStore);
        _tenantContext = tenantContext;
        _clock = clock;
        _exerciseStore = exerciseStore;
    }

    public async Task<Result> HandleAsync(
        ArchiveExerciseCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.ExerciseId == Guid.Empty)
        {
            return Result.Failure(Error.Validation([
                new ValidationError(
                    "ExerciseId",
                    "exercise_id_required",
                    "Exercise ID is required."
                )
            ]));
        }

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result.Failure(tenant.Error!);

        var outcome = await _exerciseStore.SetActiveAsync(
            command.ExerciseId,
            tenant.Value,
            false,
            _clock.UtcNow,
            cancellationToken
        );

        return outcome.Kind switch
        {
            ExerciseStoreResult.Status.Changed or
                ExerciseStoreResult.Status.AlreadyInRequestedState => Result.Success(),
            ExerciseStoreResult.Status.NotFound => Result.Failure(TrainingErrors.ExerciseNotFound),
            ExerciseStoreResult.Status.GlobalReadOnly => Result.Failure(TrainingErrors.GlobalExerciseReadOnly),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
    }
}
