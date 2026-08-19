using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Supplements.UpdateSupplementAssignment;

/// <summary>Atualiza instruções de uma atribuição do tenant.</summary>
public sealed class UpdateSupplementAssignmentHandler
{
    private readonly IValidator<UpdateSupplementAssignmentCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IClientSupplementAssignmentStore _store;

    public UpdateSupplementAssignmentHandler(
        IValidator<UpdateSupplementAssignmentCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IClientSupplementAssignmentStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<ClientSupplementAssignmentDto>> HandleAsync(
        UpdateSupplementAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<ClientSupplementAssignmentDto>.Failure(validation.ToApplicationError());

        var actor = SupplementActorAuthorization.RequireTrainer(_tenantContext);
        if (!actor.IsSuccess)
            return Result<ClientSupplementAssignmentDto>.Failure(actor.Error!);

        var outcome = await _store.UpdateInstructionsAsync(
            actor.Value.TrainerId,
            command.AssignmentId,
            command.ServingSize,
            command.Timing,
            command.TrainerNotes,
            _clock.UtcNow,
            cancellationToken);

        return outcome.ToDtoResult();
    }
}
