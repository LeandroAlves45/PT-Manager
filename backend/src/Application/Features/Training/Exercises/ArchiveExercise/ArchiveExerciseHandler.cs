using Application.Common.Abstractions;
using Application.Common.Authorization;
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
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _exerciseStore = exerciseStore ?? throw new ArgumentNullException(nameof(exerciseStore));
    }

    public async Task<Result> HandleAsync(
        ArchiveExerciseCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.ExerciseId == Guid.Empty)
            return Result.Failure(TrainingErrors.ExerciseIdRequired());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, TrainingErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        var outcome = await _exerciseStore.SetActiveAsync(
            command.ExerciseId,
            actor.Value.TrainerId,
            false,
            _clock.UtcNow,
            cancellationToken
        );

        return outcome.ToTransitionResult();
    }
}
