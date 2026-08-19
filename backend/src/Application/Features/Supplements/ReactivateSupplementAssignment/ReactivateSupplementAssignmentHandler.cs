using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Results;

namespace Application.Features.Supplements.ReactivateSupplementAssignment;

/// <summary>Reativa uma atribuição quando cliente e suplemento estão ativos.</summary>
public sealed class ReactivateSupplementAssignmentHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IClientSupplementAssignmentStore _store;

    public ReactivateSupplementAssignmentHandler(
        ITenantContext tenantContext, IClock clock, IClientSupplementAssignmentStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<ClientSupplementAssignmentDto>> HandleAsync(
        ReactivateSupplementAssignmentCommand command,
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
            true,
            _clock.UtcNow,
            cancellationToken);

        return outcome.ToDtoResult();
    }
}
