using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Training.Exercises.Abstractions;
using Application.Results;

namespace Application.Features.Training.Exercises.ArchiveGlobalExercise;

/// <summary>Arquiva um exercício global de forma auditada.</summary>
public sealed class ArchiveGlobalExerciseHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IGlobalExerciseStore _store;

    public ArchiveGlobalExerciseHandler(
        ITenantContext tenantContext, IClock clock, IGlobalExerciseStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(
        ArchiveGlobalExerciseCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ExerciseId == Guid.Empty)
            return Result.Failure(TrainingErrors.ExerciseIdRequired());

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext, TrainingErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        var outcome = await _store.SetActiveAsync(
            actor.Value.UserId,
            command.ExerciseId,
            false,
            _clock.UtcNow,
            cancellationToken);

        return outcome.ToTransitionResult();
    }
}
