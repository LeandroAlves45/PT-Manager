using Application.Common.Abstractions;
using Application.Features.Packs.ClientSessionPacks.Abstractions;
using Application.Features.Packs.ClientSessionPacks.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Packs.ClientSessionPacks.UpdateClientSessionPackExpectedEndDate;

/// <summary>Atualiza a expectativa temporal sem afetar o saldo.</summary>
public sealed class UpdateClientSessionPackExpectedEndDateHandler
{
    private readonly IValidator<UpdateClientSessionPackExpectedEndDateCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IClientSessionPackStore _store;

    public UpdateClientSessionPackExpectedEndDateHandler(
        IValidator<UpdateClientSessionPackExpectedEndDateCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        IClientSessionPackStore store
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

    public async Task<Result<ClientSessionPackDto>> HandleAsync(
        UpdateClientSessionPackExpectedEndDateCommand command,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<ClientSessionPackDto>.Failure(validation.ToApplicationError());

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<ClientSessionPackDto>.Failure(tenant.Error!);

        var outcome = await _store.UpdateExpectedEndDateAsync(
            tenant.Value,
            command.ClientSessionPackId,
            command.ExpectedEndDate,
            _clock.UtcNow,
            cancellationToken
        );

        return outcome.ToUpdateResult();
    }
}
