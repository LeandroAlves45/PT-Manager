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
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeZoneProvider = timeZoneProvider ?? throw new ArgumentNullException(nameof(timeZoneProvider));
        _store = store ?? throw new ArgumentNullException(nameof(store));
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
