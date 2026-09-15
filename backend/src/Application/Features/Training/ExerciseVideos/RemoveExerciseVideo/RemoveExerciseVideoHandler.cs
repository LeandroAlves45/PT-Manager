using Application.Common.Abstractions;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Application.Results;

namespace Application.Features.Training.ExerciseVideos.RemoveExerciseVideo;

/// <summary>
/// Remove o vídeo publicado. O objeto só é eliminado depois do commit, pelo
/// durable job agendado na mesma transação.
/// </summary>
public sealed class RemoveExerciseVideoHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IExerciseVideoStore _store;

    public RemoveExerciseVideoHandler(
        ITenantContext tenantContext,
        IClock clock,
        IExerciseVideoStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(
        RemoveExerciseVideoCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.ExerciseId == Guid.Empty)
            return Result.Failure(TrainingErrors.ExerciseIdRequired());

        var actor = ExerciseVideoActors.ResolveWriter(_tenantContext, command.Catalog);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        var outcome = await _store.RemoveReadyAsync(
            command.Catalog,
            command.ExerciseId,
            actor.Value.OwnerTrainerId,
            actor.Value.UserId,
            Guid.NewGuid(),
            _clock.UtcNow,
            cancellationToken);

        return outcome == ExerciseVideoRemovalStatus.Removed
            ? Result.Success()
            : Result.Failure(ExerciseVideoErrors.VideoNotFound);
    }
}
