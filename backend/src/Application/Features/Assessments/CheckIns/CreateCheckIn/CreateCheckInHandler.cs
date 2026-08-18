using Application.Common.Abstractions;
using Application.Features.Assessments.CheckIns.Abstractions;
using Application.Features.Assessments.CheckIns.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Assessments.CheckIns.CreateCheckIn;

/// <summary>Agenda um check-in pelo personal trainer autenticado.</summary>
public sealed class CreateCheckInHandler
{
    private readonly IValidator<CreateCheckInCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;
    private readonly ICheckInStore _store;

    public CreateCheckInHandler(
        IValidator<CreateCheckInCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        ITrainerTimeZoneProvider timeZoneProvider,
        ICheckInStore store
    )
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(timeZoneProvider);
        ArgumentNullException.ThrowIfNull(store);
        _validator = validator;
        _tenantContext = tenantContext;
        _clock = clock;
        _timeZoneProvider = timeZoneProvider;
        _store = store;
    }

    public async Task<Result<CheckInDto>> HandleAsync(
        CreateCheckInCommand command,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<CheckInDto>.Failure(validation.ToApplicationError());

        var tenant = AssessmentActorAuthorization.RequireTrainer(_tenantContext);
        if (!tenant.IsSuccess)
            return Result<CheckInDto>.Failure(tenant.Error!);

        var now = _clock.UtcNow;
        var timeZone = await _timeZoneProvider.GetRequiredAsync(tenant.Value, cancellationToken);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, timeZone));

        var outcome = await _store.CreateAsync(
            tenant.Value,
            command.ClientId,
            command.CheckInDate,
            command.TargetDate,
            now,
            cancellationToken
        );

        return outcome.ToResult(today);
    }
}
