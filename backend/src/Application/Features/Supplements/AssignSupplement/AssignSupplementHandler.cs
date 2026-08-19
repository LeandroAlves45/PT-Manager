using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Supplements.AssignSupplement;

/// <summary>Atribui um suplemento ativo a um cliente ativo.</summary>
public sealed class AssignSupplementHandler
{
    private readonly IValidator<AssignSupplementCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IClientSupplementAssignmentStore _store;

    public AssignSupplementHandler(
        IValidator<AssignSupplementCommand> validator,
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
        AssignSupplementCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<ClientSupplementAssignmentDto>.Failure(validation.ToApplicationError());

        var actor = SupplementActorAuthorization.RequireTrainer(_tenantContext);
        if (!actor.IsSuccess)
            return Result<ClientSupplementAssignmentDto>.Failure(actor.Error!);

        var outcome = await _store.AssignAsync(
            actor.Value.TrainerId,
            command.ClientId,
            command.SupplementId,
            command.ServingSize,
            command.Timing,
            command.TrainerNotes,
            _clock.UtcNow,
            cancellationToken);

        return outcome.ToDtoResult();
    }
}
