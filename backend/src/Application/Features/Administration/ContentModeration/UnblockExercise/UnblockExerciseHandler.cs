using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Errors;
using Application.Features.Administration.ContentModeration.Abstractions;
using Application.Results;

namespace Application.Features.Administration.ContentModeration.UnblockExercise;

/// <summary>Remove o bloqueio sem alterar a disponibilidade escolhida pelo trainer.</summary>
public sealed class UnblockExerciseHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IPrivateCatalogModerationStore _store;

    public UnblockExerciseHandler(
        ITenantContext tenantContext,
        IClock clock,
        IPrivateCatalogModerationStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(
        UnblockExerciseCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ExerciseId == Guid.Empty)
            return Result.Failure(Error.Validation([
                new ValidationError(
                    "ExerciseId",
                    "exercise_id_required",
                    "Exercise ID is required."
                )
            ]));

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext,
            ContentModerationErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        return (await _store.UnblockExerciseAsync(
            actor.Value.UserId,
            command.ExerciseId,
            _clock.UtcNow,
            cancellationToken))
            .ToResult();
    }
}
