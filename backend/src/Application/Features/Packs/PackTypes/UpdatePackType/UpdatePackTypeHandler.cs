using Application.Common.Abstractions;
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
        UpdatePackTypeCommand command,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<PackTypeDto>.Failure(validation.ToApplicationError());

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<PackTypeDto>.Failure(tenant.Error!);

        var outcome = await _store.UpdateAsync(
            command.PackTypeId,
            tenant.Value,
            command.Name,
            command.SessionCount,
            command.PriceCents,
            command.Currency,
            command.ExpectedDurationDays,
            _clock.UtcNow,
            cancellationToken
        );

        return outcome.Kind switch
        {
            PackTypeStoreResult.Status.Updated =>
                Result<PackTypeDto>.Success(outcome.PackType!.ToDto()),
            PackTypeStoreResult.Status.NotFound =>
                Result<PackTypeDto>.Failure(PackErrors.PackTypeNotFound),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
    }
}
