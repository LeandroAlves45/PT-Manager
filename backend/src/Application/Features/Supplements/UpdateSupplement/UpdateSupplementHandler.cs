using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Supplements.UpdateSupplement;

/// <summary>Atualiza um suplemento privado sem permitir escrita global.</summary>
public sealed class UpdateSupplementHandler
{
    private readonly IValidator<UpdateSupplementCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ISupplementStore _store;

    public UpdateSupplementHandler(
        IValidator<UpdateSupplementCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        ISupplementStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<SupplementDto>> HandleAsync(
        UpdateSupplementCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<SupplementDto>.Failure(validation.ToApplicationError());

        var actor = SupplementActorAuthorization.RequireTrainer(_tenantContext);
        if (!actor.IsSuccess)
            return Result<SupplementDto>.Failure(actor.Error!);

        var outcome = await _store.UpdateAsync(
            actor.Value.TrainerId,
            command.SupplementId,
            command.Name,
            command.Description,
            command.UnitOfMeasure,
            command.ServingSize,
            command.Timing,
            command.TrainerNotes,
            _clock.UtcNow,
            cancellationToken);

        return outcome.ToDtoResult();
    }
}
