using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Common.Time;
using Application.Features.Clients.Abstractions;
using Application.Features.Clients.Dtos;
using Application.Results;
using Domain.Services;

namespace Application.Features.Clients.GetClientSummary;

/// <summary>
/// Compõe o resumo de progresso do cliente: peso e variação, altura, plano ativo, adesão ao
/// treino, metas de nutrição e saldo de packs. A adesão usa o calendário cíclico do plano
/// (<see cref="TrainingPlanSchedule"/>), pelo que só conta séries feitas no dia previsto.
/// </summary>
public sealed class GetClientSummaryHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;
    private readonly IClientProgressSummaryQueries _queries;

    public GetClientSummaryHandler(
        ITenantContext tenantContext,
        IClock clock,
        ITrainerTimeZoneProvider timeZoneProvider,
        IClientProgressSummaryQueries queries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeZoneProvider = timeZoneProvider ?? throw new ArgumentNullException(nameof(timeZoneProvider));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<ClientProgressSummaryDto>> HandleAsync(
        GetClientSummaryQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var actor = ActorAuthorization.RequireTrainer(
            _tenantContext,
            ClientErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<ClientProgressSummaryDto>.Failure(actor.Error!);

        if (query.ClientId == Guid.Empty)
            return Result<ClientProgressSummaryDto>.Failure(ClientErrors.ClientNotFound);

        var timeZone = await _timeZoneProvider.GetRequiredAsync(
            actor.Value.TrainerId, cancellationToken);
        var localToday = LocalDates.Today(_clock.UtcNow, timeZone);
        var adherenceStart = localToday.AddDays(-(ClientSummaryWindows.AdherenceDays - 1));
        var logsRange = LocalDates.ToUtcRange(adherenceStart, localToday.AddDays(1), timeZone);

        var snapshot = await _queries.GetAsync(
            query.ClientId,
            localToday.AddDays(-ClientSummaryWindows.WeightChangeDays),
            logsRange.StartUtc,
            logsRange.EndUtc,
            cancellationToken);

        if (snapshot is null)
            return Result<ClientProgressSummaryDto>.Failure(ClientErrors.ClientNotFound);

        var plan = snapshot.ActiveTrainingPlan;

        return Result<ClientProgressSummaryDto>.Success(new ClientProgressSummaryDto(
            snapshot.ClientId,
            localToday,
            BuildWeight(snapshot, timeZone),
            snapshot.InitialAssessment?.HeightCm,
            plan is null
                ? null
                : new ClientProgressSummaryDto.TrainingPlanDto(
                    plan.Id,
                    plan.Name,
                    plan.StartDate,
                    plan.EndDate,
                    DaysPerWeek(plan)),
            BuildAdherence(snapshot, timeZone, adherenceStart, localToday),
            snapshot.ActiveMealPlan,
            snapshot.Packs));
    }

    private static ClientProgressSummaryDto.WeightDto? BuildWeight(
        ClientProgressSnapshot snapshot,
        TimeZoneInfo timeZone)
    {
        if (snapshot.LatestCheckInWeight is { } latest)
        {
            // A referência só conta se for um check-in anterior ao atual dentro da janela.
            var reference = snapshot.EarliestCheckInWeightInWindow;
            var hasReference = reference is not null && reference.CheckedInDate < latest.CheckedInDate;

            return new ClientProgressSummaryDto.WeightDto(
                latest.WeightKg,
                latest.CheckedInDate,
                ClientProgressSummaryDto.WeightSourceCheckIn,
                hasReference ? latest.WeightKg - reference!.WeightKg : null,
                hasReference ? reference!.CheckedInDate : null);
        }

        if (snapshot.InitialAssessment is { } initial)
        {
            return new ClientProgressSummaryDto.WeightDto(
                initial.WeightKg,
                LocalDates.ToLocalDate(initial.CreatedAt, timeZone),
                ClientProgressSummaryDto.WeightSourceInitialAssessment,
                null,
                null);
        }

        return null;
    }

    private static ClientProgressSummaryDto.AdherenceDto? BuildAdherence(
        ClientProgressSnapshot snapshot,
        TimeZoneInfo timeZone,
        DateOnly windowStart,
        DateOnly windowEnd)
    {
        if (snapshot.ActiveTrainingPlan is not { } plan)
            return null;

        // A janela nunca começa antes do plano: semanas sem plano ficam fora do denominador.
        var effectiveStart = windowStart < plan.StartDate ? plan.StartDate : windowStart;
        var effectiveEnd = plan.EndDate.HasValue && plan.EndDate.Value < windowEnd
            ? plan.EndDate.Value
            : windowEnd;

        var adherence = TrainingAdherenceCalculator.Calculate(
            plan.StartDate,
            plan.EndDate,
            plan.Days
                .Select(day => new PlannedTrainingDay(
                    day.WeekNumber,
                    day.DayOfWeek,
                    day.Sets
                        .Select(set => new PlannedSetRef(set.PrescriptionId, set.SetNumber))
                        .ToList()))
                .ToList(),
            snapshot.SetLogs.Select(log => new PerformedSetRef(
                log.PrescriptionId,
                log.SetNumber,
                LocalDates.ToLocalDate(log.PerformedAt, timeZone))),
            effectiveStart,
            effectiveEnd);

        return new ClientProgressSummaryDto.AdherenceDto(
            effectiveStart,
            effectiveEnd,
            adherence.PlannedSets,
            adherence.LoggedSets,
            adherence.Percentage);
    }

    private static int DaysPerWeek(ActiveTrainingPlanStructure plan) =>
        plan.Days.Count == 0
            ? 0
            : plan.Days
                .GroupBy(day => day.WeekNumber)
                .Max(week => week.Select(day => day.DayOfWeek).Distinct().Count());

}
