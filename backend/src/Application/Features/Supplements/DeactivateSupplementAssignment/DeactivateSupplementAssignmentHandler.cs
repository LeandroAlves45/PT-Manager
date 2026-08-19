using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Results;

namespace Application.Features.Supplements.DeactivateSupplementAssignment;

/// <summary>Desativa uma atribuição do tenant de forma idempotente.</summary>
public sealed class DeactivateSupplementAssignmentHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IClientSupplementAssignmentStore _store;

    public DeactivateSupplementAssignmentHandler(
        ITenantContext tenantContext, IClock clock, IClientSupplementAssignmentStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<ClientSupplementAssignmentDto>> HandleAsync(
        DeactivateSupplementAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        if (command.AssignmentId == Guid.Empty)
            return Result<ClientSupplementAssignmentDto>.Failure(
                SupplementErrors.AssignmentIdRequired);

        var actor = SupplementActorAuthorization.RequireTrainer(_tenantContext);
        if (!actor.IsSuccess)
            return Result<ClientSupplementAssignmentDto>.Failure(actor.Error!);

        var outcome = await _store.SetActiveAsync(
            actor.Value.TrainerId,
            command.AssignmentId,
            false,
            _clock.UtcNow,
            cancellationToken);

        return outcome.ToDtoResult();
    }
}
