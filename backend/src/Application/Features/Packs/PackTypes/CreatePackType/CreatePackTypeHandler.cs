using Application.Common.Abstractions;
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
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(store);
        _validator = validator;
        _tenantContext = tenantContext;
        _clock = clock;
        _store = store;
    }

    public async Task<Result<PackTypeDto>> HandleAsync(
        CreatePackTypeCommand command,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<PackTypeDto>.Failure(validation.ToApplicationError());

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<PackTypeDto>.Failure(tenant.Error!);

        var packType = new PackType(
            tenant.Value,
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
