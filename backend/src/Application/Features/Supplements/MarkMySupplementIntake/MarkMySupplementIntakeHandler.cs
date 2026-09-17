using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Results;

namespace Application.Features.Supplements.MarkMySupplementIntake;

/// <summary>Regista a toma de hoje; repetir o pedido devolve a mesma toma.</summary>
public sealed class MarkMySupplementIntakeHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ISupplementIntakeStore _store;

    public MarkMySupplementIntakeHandler(
        ITenantContext tenantContext,
        IClock clock,
        ISupplementIntakeStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<MySupplementIntakeDto>> HandleAsync(
        MarkMySupplementIntakeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.AssignmentId == Guid.Empty)
            return Result<MySupplementIntakeDto>.Failure(SupplementErrors.AssignmentIdRequired);

        var actor = SupplementActorAuthorization.RequireClient(_tenantContext);
        if (!actor.IsSuccess)
            return Result<MySupplementIntakeDto>.Failure(actor.Error!);

        var outcome = await _store.MarkAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            command.AssignmentId,
            DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc),
            cancellationToken);

        return outcome.Kind switch
        {
            SupplementIntakeStoreResult.Status.Marked or
            SupplementIntakeStoreResult.Status.AlreadyMarked =>
                Result<MySupplementIntakeDto>.Success(new MySupplementIntakeDto(
                    outcome.Intake!.ClientSupplementAssignmentId,
                    outcome.Intake.LocalDate,
                    outcome.Intake.TakenAt)),
            SupplementIntakeStoreResult.Status.AssignmentInactive =>
                Result<MySupplementIntakeDto>.Failure(SupplementErrors.AssignmentInactive),
            SupplementIntakeStoreResult.Status.AssignmentNotFound =>
                Result<MySupplementIntakeDto>.Failure(SupplementErrors.AssignmentNotFound),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };
    }
}
