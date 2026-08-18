using Application.Common.Abstractions;
using Application.Features.Assessments.CheckIns.Abstractions;
using Application.Features.Assessments.CheckIns.Dtos;
using Application.Results;

namespace Application.Features.Assessments.CheckIns.CancelCheckIn;

/// <summary>Cancela um check-in antes do dia agendado.</summary>
public sealed class CancelCheckInHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;
    private readonly ICheckInStore _store;

    public CancelCheckInHandler(
        ITenantContext tenantContext,
        IClock clock,
        ITrainerTimeZoneProvider timeZoneProvider,
        ICheckInStore store
    )
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(timeZoneProvider);
        ArgumentNullException.ThrowIfNull(store);
        _tenantContext = tenantContext;
        _clock = clock;
        _timeZoneProvider = timeZoneProvider;
        _store = store;
    }

    public async Task<Result<CheckInDto>> HandleAsync(
        CancelCheckInCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenant = AssessmentActorAuthorization.RequireTrainer(_tenantContext);
        if (!tenant.IsSuccess)
            return Result<CheckInDto>.Failure(tenant.Error!);

        var now = _clock.UtcNow;
        var timeZone = await _timeZoneProvider.GetRequiredAsync(tenant.Value, cancellationToken);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, timeZone));

        var outcome = await _store.CancelAsync(
            tenant.Value,
            command.CheckInId,
            now,
            cancellationToken
        );

        return outcome.ToResult(today);
    }
}
