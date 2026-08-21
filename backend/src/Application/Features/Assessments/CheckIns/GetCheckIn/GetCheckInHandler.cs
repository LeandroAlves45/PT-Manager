using Application.Common.Abstractions;
using Application.Features.Assessments.CheckIns.Abstractions;
using Application.Features.Assessments.CheckIns.Dtos;
using Application.Results;

namespace Application.Features.Assessments.CheckIns.GetCheckIn;

/// <summary>Obtém um check-in visível ao personal trainer.</summary>
public sealed class GetCheckInHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;
    private readonly ICheckInQueries _queries;

    public GetCheckInHandler(
        ITenantContext tenantContext,
        IClock clock,
        ITrainerTimeZoneProvider timeZoneProvider,
        ICheckInQueries queries
    )
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeZoneProvider = timeZoneProvider ?? throw new ArgumentNullException(nameof(timeZoneProvider));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<CheckInDto>> HandleAsync(
        GetCheckInQuery query,
        CancellationToken cancellationToken
    )
    {
        var tenant = AssessmentActorAuthorization.RequireTrainer(_tenantContext);
        if (!tenant.IsSuccess)
            return Result<CheckInDto>.Failure(tenant.Error!);

        var timeZone = await _timeZoneProvider.GetRequiredAsync(tenant.Value, cancellationToken);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow, timeZone));
        var checkIn = await _queries.GetAsync(
            tenant.Value,
            query.CheckInId,
            today,
            cancellationToken
        );

        return checkIn is null
            ? Result<CheckInDto>.Failure(AssessmentErrors.CheckInNotFound)
            : Result<CheckInDto>.Success(checkIn);
    }
}
