using Application.Common.Abstractions;
using Application.Errors;
using Application.Features.Clients;
using Application.Features.Packs.ClientSessionPacks.Abstractions;
using Application.Features.Packs.ClientSessionPacks.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Packs.ClientSessionPacks.AssignClientSessionPack;

/// <summary>Atribui um PackType ativo a um cliente do tenant.</summary>
public sealed class AssignClientSessionPackHandler
{
    private readonly IValidator<AssignClientSessionPackCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;
    private readonly IClock _clock;
    private readonly IClientSessionPackStore _store;

    public AssignClientSessionPackHandler(
        IValidator<AssignClientSessionPackCommand> validator,
        ITenantContext tenantContext,
        ITrainerTimeZoneProvider timeZoneProvider,
        IClock clock,
        IClientSessionPackStore store
    )
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(timeZoneProvider);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(store);

        _validator = validator;
        _tenantContext = tenantContext;
        _timeZoneProvider = timeZoneProvider;
        _clock = clock;
        _store = store;
    }

    public async Task<Result<ClientSessionPackDto>> HandleAsync(
        AssignClientSessionPackCommand command,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<ClientSessionPackDto>.Failure(validation.ToApplicationError());

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<ClientSessionPackDto>.Failure(tenant.Error!);

        var now = _clock.UtcNow;
        var timezone = await _timeZoneProvider.GetRequiredAsync(
            tenant.Value,
            cancellationToken
        );
        var localToday = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(now, timezone)
        );

        if (command.PurchaseDate > localToday)
            return Result<ClientSessionPackDto>.Failure(
                Error.Validation(
                [
                    new ValidationError(
                        "PurchaseDate",
                        "purchase_date_future",
                        "Purchase date cannot be in the future."
                    )
                ])
            );

        var outcome = await _store.AssignAsync(
            tenant.Value,
            command.ClientId,
            command.PackTypeId,
            command.PurchaseDate,
            command.ExpectedEndDate,
            now,
            cancellationToken
        );

        return outcome.ToAssignResult();
    }
}
