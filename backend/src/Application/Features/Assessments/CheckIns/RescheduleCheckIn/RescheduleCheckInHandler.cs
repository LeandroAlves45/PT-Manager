using Application.Common.Abstractions;
using Application.Features.Assessments.CheckIns.Abstractions;
using Application.Features.Assessments.CheckIns.Dtos;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Assessments.CheckIns.RescheduleCheckIn;

/// <summary>Reagenda um check-in ainda aberto e futuro.</summary>
public sealed class RescheduleCheckInHandler
{
    private readonly IValidator<RescheduleCheckInCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;
    private readonly ICheckInStore _store;

    public RescheduleCheckInHandler(
        IValidator<RescheduleCheckInCommand> validator,
        ITenantContext tenantContext,
        IClock clock,
        ITrainerTimeZoneProvider timeZoneProvider,
        ICheckInStore store
    )
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeZoneProvider = timeZoneProvider ?? throw new ArgumentNullException(nameof(timeZoneProvider));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<CheckInDto>> HandleAsync(
        RescheduleCheckInCommand command,
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

        var outcome = await _store.RescheduleAsync(
            tenant.Value,
            command.CheckInId,
            command.CheckInDate,
            command.TargetDate,
            now,
            cancellationToken
        );

        return outcome.ToResult(today);
    }
}
