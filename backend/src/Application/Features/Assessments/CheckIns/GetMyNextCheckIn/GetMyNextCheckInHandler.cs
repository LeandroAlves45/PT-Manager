using Application.Common.Abstractions;
using Application.Common.Time;
using Application.Features.Assessments.CheckIns.Abstractions;
using Application.Features.Assessments.CheckIns.Dtos;
using Application.Results;

namespace Application.Features.Assessments.CheckIns.GetMyNextCheckIn;

/// <summary>Pede o próximo check-in agendado do cliente autenticado.</summary>
public sealed record GetMyNextCheckInQuery;

/// <summary>
/// Devolve o próximo check-in agendado (hoje ou no futuro) que ainda não foi respondido nem
/// cancelado. Null é resultado válido: pode não haver nenhum agendado.
/// </summary>
public sealed class GetMyNextCheckInHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;
    private readonly ICheckInQueries _queries;

    public GetMyNextCheckInHandler(
        ITenantContext tenantContext,
        IClock clock,
        ITrainerTimeZoneProvider timeZoneProvider,
        ICheckInQueries queries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeZoneProvider = timeZoneProvider ?? throw new ArgumentNullException(nameof(timeZoneProvider));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<MyNextCheckInDto?>> HandleAsync(
        GetMyNextCheckInQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var actor = AssessmentActorAuthorization.RequireClient(_tenantContext);
        if (!actor.IsSuccess)
            return Result<MyNextCheckInDto?>.Failure(actor.Error!);

        var timeZone = await _timeZoneProvider.GetRequiredAsync(actor.Value.TrainerId, cancellationToken);
        var localToday = LocalDates.Today(_clock.UtcNow, timeZone);

        var next = await _queries.GetMyNextAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            localToday,
            cancellationToken);

        return Result<MyNextCheckInDto?>.Success(next);
    }
}
