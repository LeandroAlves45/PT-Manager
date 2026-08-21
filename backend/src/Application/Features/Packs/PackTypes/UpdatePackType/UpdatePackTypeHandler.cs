using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Packs.PackTypes.Abstractions;
using Application.Features.Packs.PackTypes.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Packs.PackTypes.UpdatePackType;

/// <summary>Atualiza um tipo de pack privado do tenant.</summary>
public sealed class UpdatePackTypeHandler
{
    private readonly IValidator<UpdatePackTypeCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IPackTypeStore _store;

    public UpdatePackTypeHandler(
        IValidator<UpdatePackTypeCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IPackTypeStore store
    )
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<PackTypeDto>> HandleAsync(
        UpdatePackTypeCommand command,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<PackTypeDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, PackErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<PackTypeDto>.Failure(actor.Error!);

        var outcome = await _store.UpdateAsync(
            command.PackTypeId,
            actor.Value.TrainerId,
            command.Name,
            command.SessionCount,
            command.PriceCents,
            command.Currency,
            command.ExpectedDurationDays,
            _clock.UtcNow,
            cancellationToken
        );

        return outcome.ToUpdateResult();
    }
}
