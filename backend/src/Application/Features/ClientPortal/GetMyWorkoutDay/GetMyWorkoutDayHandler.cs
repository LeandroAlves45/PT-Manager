using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Common.Time;
using Application.Features.ClientPortal.Dtos;
using Application.Features.ClientPortal.GetMyWorkoutToday;
using Application.Results;

namespace Application.Features.ClientPortal.GetMyWorkoutDay;

/// <summary>Pede o treino de um dia passado do plano ativo, só para ler.</summary>
public sealed record GetMyWorkoutDayQuery(DateOnly? Date);

/// <summary>
/// Devolve um dia passado do plano de treino ativo, com o planeado, o que o cliente registou,
/// as notas das séries e a nota de conclusão. O dia de hoje fica no endpoint próprio, que é o
/// único onde se regista: hoje, dias futuros e dias fora do plano são recusados.
/// </summary>
public sealed class GetMyWorkoutDayHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;
    private readonly MyWorkoutTodayReader _reader;

    public GetMyWorkoutDayHandler(
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
        GetMyWorkoutDayQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var actor = ActorAuthorization.RequireClient(_tenantContext, ClientPortalErrors.ClientOnly);
        if (!actor.IsSuccess)
            return Result<MyWorkoutTodayDto>.Failure(actor.Error!);

        if (query.Date is not { } date)
            return Result<MyWorkoutTodayDto>.Failure(ClientPortalErrors.WorkoutDayNotAvailable);

        var timeZone = await _timeZoneProvider.GetRequiredAsync(actor.Value.TrainerId, cancellationToken);
        var today = LocalDates.Today(_clock.UtcNow, timeZone);
        if (date >= today)
            return Result<MyWorkoutTodayDto>.Failure(ClientPortalErrors.WorkoutDayNotAvailable);

        var workout = await _reader.ReadAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            date,
            timeZone,
            cancellationToken);

        if (workout is null)
            return Result<MyWorkoutTodayDto>.Failure(ClientPortalErrors.TrainingPlanNotAvailable);

        // Um dia antes do início (ou depois do fim) do plano não é um dia do plano.
        return workout.Status == MyWorkoutTodayStatus.OutsidePlan
            ? Result<MyWorkoutTodayDto>.Failure(ClientPortalErrors.WorkoutDayNotAvailable)
            : Result<MyWorkoutTodayDto>.Success(workout with { NextWorkout = null });
    }
}
