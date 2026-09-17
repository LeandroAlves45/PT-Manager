using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Results;

namespace Application.Features.Supplements.UnmarkMySupplementIntake;

/// <summary>Remove a toma de hoje; sem toma registada responde com sucesso (idempotente).</summary>
public sealed class UnmarkMySupplementIntakeHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ISupplementIntakeStore _store;

    public UnmarkMySupplementIntakeHandler(
        ITenantContext tenantContext,
        IClock clock,
        ISupplementIntakeStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result> HandleAsync(
        UnmarkMySupplementIntakeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.AssignmentId == Guid.Empty)
            return Result.Failure(SupplementErrors.AssignmentIdRequired);

        var actor = SupplementActorAuthorization.RequireClient(_tenantContext);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error!);

        var outcome = await _store.UnmarkAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            command.AssignmentId,
            DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc),
            cancellationToken);

        return outcome.Kind switch
        {
            SupplementIntakeStoreResult.Status.Unmarked or
            SupplementIntakeStoreResult.Status.NotMarked => Result.Success(),
            SupplementIntakeStoreResult.Status.AssignmentNotFound =>
                Result.Failure(SupplementErrors.AssignmentNotFound),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };
    }
}
