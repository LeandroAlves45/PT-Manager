using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.Packs.PackTypes.Abstractions;
using Application.Features.Packs.PackTypes.Dtos;
using Application.Results;
using Application.Validation;
using Domain.Entities.Billing;
using FluentValidation;

namespace Application.Features.Packs.PackTypes.CreatePackType;

/// <summary>Cria um tipo de pack no tenant autenticado.</summary>
public sealed class CreatePackTypeHandler
{
    private readonly IValidator<CreatePackTypeCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IPackTypeStore _store;

    public CreatePackTypeHandler(
        IValidator<CreatePackTypeCommand> validator,
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
        CreatePackTypeCommand command,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<PackTypeDto>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, PackErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<PackTypeDto>.Failure(actor.Error!);

        var packType = new PackType(
            actor.Value.TrainerId,
            command.Name,
            command.SessionCount,
            command.PriceCents,
            command.Currency,
            command.ExpectedDurationDays,
            _clock.UtcNow
        );

        await _store.AddAsync(packType, cancellationToken);
        return Result<PackTypeDto>.Success(packType.ToDto());
    }
}
