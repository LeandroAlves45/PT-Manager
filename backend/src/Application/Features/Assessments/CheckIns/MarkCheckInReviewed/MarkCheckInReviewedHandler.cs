using Application.Common.Abstractions;
using Application.Features.Assessments.CheckIns.Abstractions;
using Application.Features.Assessments.CheckIns.Dtos;
using Application.Results;

namespace Application.Features.Assessments.CheckIns.MarkCheckInReviewed;

/// <summary>
/// Marca uma resposta de check-in como revista pelo personal trainer. Idempotente: repetir
/// devolve o check-in com o instante original de revisão.
/// </summary>
public sealed class MarkCheckInReviewedHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;
    private readonly ICheckInStore _store;

    public MarkCheckInReviewedHandler(
        ITenantContext tenantContext,
        IClock clock,
        ITrainerTimeZoneProvider timeZoneProvider,
        ICheckInStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeZoneProvider = timeZoneProvider ?? throw new ArgumentNullException(nameof(timeZoneProvider));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<CheckInDto>> HandleAsync(
        MarkCheckInReviewedCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.CheckInId == Guid.Empty)
            return Result<CheckInDto>.Failure(AssessmentErrors.CheckInIdRequired);

        var tenant = AssessmentActorAuthorization.RequireTrainer(_tenantContext);
        if (!tenant.IsSuccess)
            return Result<CheckInDto>.Failure(tenant.Error!);

        var now = _clock.UtcNow;
        var timeZone = await _timeZoneProvider.GetRequiredAsync(tenant.Value, cancellationToken);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, timeZone));

        var outcome = await _store.MarkReviewedAsync(
            tenant.Value,
            command.CheckInId,
            now,
            cancellationToken);

        return outcome.ToResult(today);
    }
}
