using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Common.Time;
using Application.Features.ClientPortal.Dtos;
using Application.Results;

namespace Application.Features.ClientPortal.GetMyWorkoutToday;

/// <summary>Pede o treino de hoje do cliente autenticado.</summary>
public sealed record GetMyWorkoutTodayQuery;

/// <summary>
/// Resolve o dia local do personal trainer e delega a composição no "MyWorkoutTodayReader".
/// Sem plano devolve o mesmo status NotFound do endpoint "/portal/my-plan", para não revelar se
/// o cliente existe ou não está arquivado.
/// </summary>
public sealed class GetMyWorkoutTodayHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;
    private readonly MyWorkoutTodayReader _reader;

    public GetMyWorkoutTodayHandler(
        ITenantContext tenantContext,
        IClock clock,
        ITrainerTimeZoneProvider timeZoneProvider,
        MyWorkoutTodayReader reader)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeZoneProvider = timeZoneProvider ?? throw new ArgumentNullException(nameof(timeZoneProvider));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public async Task<Result<MyWorkoutTodayDto>> HandleAsync(
        GetMyWorkoutTodayQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var actor = ActorAuthorization.RequireClient(_tenantContext, ClientPortalErrors.ClientOnly);
        if (!actor.IsSuccess)
            return Result<MyWorkoutTodayDto>.Failure(actor.Error!);

        var timeZone = await _timeZoneProvider.GetRequiredAsync(actor.Value.TrainerId, cancellationToken);
        var localDate = LocalDates.Today(_clock.UtcNow, timeZone);
        var range = LocalDates.ToUtcRange(localDate, localDate.AddDays(1), timeZone);

        var workout = await _reader.ReadAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            localDate,
            range.StartUtc,
            range.EndUtc,
            cancellationToken);

        return workout is null
            ? Result<MyWorkoutTodayDto>.Failure(ClientPortalErrors.TrainingPlanNotAvailable)
            : Result<MyWorkoutTodayDto>.Success(workout);
    }
}
