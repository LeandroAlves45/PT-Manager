using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Supplements.UpdateGlobalSupplement;

/// <summary>Atualiza um suplemento global e grava snapshots de auditoria.</summary>
public sealed class UpdateGlobalSupplementHandler
{
    private readonly IValidator<UpdateGlobalSupplementCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IGlobalSupplementStore _store;

    public UpdateGlobalSupplementHandler(
        IValidator<UpdateGlobalSupplementCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IGlobalSupplementStore store)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<GlobalSupplementDto>> HandleAsync(
        UpdateGlobalSupplementCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<GlobalSupplementDto>.Failure(validation.ToApplicationError());

        var actor = SupplementActorAuthorization.RequireAdministrator(_tenantContext);
        if (!actor.IsSuccess)
            return Result<GlobalSupplementDto>.Failure(actor.Error!);

        var outcome = await _store.UpdateAsync(
            actor.Value.UserId,
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
