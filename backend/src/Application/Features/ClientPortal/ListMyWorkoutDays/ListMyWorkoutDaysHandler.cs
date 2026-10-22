using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Common.Time;
using Application.Features.ClientPortal.Abstractions;
using Application.Results;
using Domain.Services;

namespace Application.Features.ClientPortal.ListMyWorkoutDays;

/// <summary>Dia passado com treino no calendário do plano ativo.</summary>
public sealed record MyPastWorkoutDayDto(DateOnly Date, int WeekNumber, int DayOfWeek);

/// <summary>Lista os dias passados com treino do plano ativo do cliente autenticado.</summary>
public sealed record ListMyWorkoutDaysQuery;

/// <summary>
/// Devolve os dias com treino do plano ativo, do início do plano até ontem no fuso do personal
/// trainer, do mais recente para o mais antigo. Alimenta o filtro de dia do portal: só dias já
/// passados do plano ativo, nunca dias futuros nem planos arquivados.
/// </summary>
public sealed class ListMyWorkoutDaysHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;
    private readonly IMyTrainingPlanQueries _planQueries;

    public ListMyWorkoutDaysHandler(
        ITenantContext tenantContext,
        IClock clock,
        ITrainerTimeZoneProvider timeZoneProvider,
        IMyTrainingPlanQueries planQueries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeZoneProvider = timeZoneProvider ?? throw new ArgumentNullException(nameof(timeZoneProvider));
        _planQueries = planQueries ?? throw new ArgumentNullException(nameof(planQueries));
    }

    public async Task<Result<IReadOnlyList<MyPastWorkoutDayDto>>> HandleAsync(
        ListMyWorkoutDaysQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var actor = ActorAuthorization.RequireClient(_tenantContext, ClientPortalErrors.ClientOnly);
        if (!actor.IsSuccess)
            return Result<IReadOnlyList<MyPastWorkoutDayDto>>.Failure(actor.Error!);

        var plan = await _planQueries.GetActiveAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            cancellationToken);
        if (plan is null)
            return Result<IReadOnlyList<MyPastWorkoutDayDto>>.Failure(
                ClientPortalErrors.TrainingPlanNotAvailable);

        var timeZone = await _timeZoneProvider.GetRequiredAsync(actor.Value.TrainerId, cancellationToken);
        var yesterday = LocalDates.Today(_clock.UtcNow, timeZone).AddDays(-1);
        var last = plan.EndDate is { } end && end < yesterday ? end : yesterday;

        var cycleLength = plan.Days.Count == 0 ? 1 : plan.Days.Max(day => day.WeekNumber);
        var slots = plan.Days
            .Select(day => new ScheduledSlot(day.WeekNumber, day.DayOfWeek))
            .ToHashSet();

        var days = new List<MyPastWorkoutDayDto>();
        for (var date = last; date >= plan.StartDate; date = date.AddDays(-1))
        {
            var slot = TrainingPlanSchedule.Resolve(plan.StartDate, plan.EndDate, cycleLength, date);
            if (slot is { } value && slots.Contains(value))
                days.Add(new MyPastWorkoutDayDto(date, value.WeekNumber, value.DayOfWeek));
        }

        return Result<IReadOnlyList<MyPastWorkoutDayDto>>.Success(days);
    }
}
