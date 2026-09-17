using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Training.ExerciseSetLogs.Abstractions;
using Application.Results;

namespace Application.Features.Training.ExerciseSetLogs.DeleteMyExerciseSetLog;

/// <summary>
/// Desmarca uma série do cliente autenticado. Só hoje e antes de concluir o treino do dia;
/// depois disso o histórico só é corrigido pelo personal trainer.
/// </summary>
public sealed class DeleteMyExerciseSetLogHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IMyExerciseSetLogStore _store;

    public DeleteMyExerciseSetLogHandler(
        ITenantContext tenantContext,
        IClock clock,
        IMyExerciseSetLogStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(
        DeleteMyExerciseSetLogCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ExerciseSetLogId == Guid.Empty)
            return Result.Failure(TrainingErrors.ExerciseSetLogIdRequired);

        var actor = ActorAuthorization.RequireClient(
            _tenantContext,
            TrainingErrors.TrainingClientOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        var outcome = await _store.DeleteAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            command.ExerciseSetLogId,
            DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc),
            cancellationToken);

        return outcome.ToResult();
    }
}
