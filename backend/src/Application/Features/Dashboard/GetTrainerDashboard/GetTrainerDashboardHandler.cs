using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Common.Time;
using Application.Features.Dashboard.Abstractions;
using Application.Features.Dashboard.Dtos;
using Application.Results;

namespace Application.Features.Dashboard.GetTrainerDashboard;

/// <summary>
/// Resolve "hoje" e "mês" no fuso horário do personal trainer, lê os blocos agregados numa só ida
/// á Infrastructure e converte o "sem plano desde" para os dias locais.
/// </summary>
public sealed class GetTrainerDashboardHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;
    private readonly ITrainerDashboardQueries _queries;

    public GetTrainerDashboardHandler(
        ITenantContext tenantContext,
        IClock clock,
        ITrainerTimeZoneProvider timeZoneProvider,
        ITrainerDashboardQueries queries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeZoneProvider = timeZoneProvider ?? throw new ArgumentNullException(nameof(timeZoneProvider));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<TrainerDashboardDto>> HandleAsync(
        GetTrainerDashboardQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var actor = ActorAuthorization.RequireTrainer(
            _tenantContext,
            DashboardErrors.TrainerOnly);
        if (!actor.IsSuccess)
            return Result<TrainerDashboardDto>.Failure(actor.Error!);

        var timeZone = await _timeZoneProvider.GetRequiredAsync(
            actor.Value.TrainerId, cancellationToken);
        var nowUtc = _clock.UtcNow;
        var localToday = LocalDates.Today(nowUtc, timeZone);
        var today = LocalDates.ToUtcRange(localToday, localToday.AddDays(1), timeZone);
        var currentMonthStart = new DateOnly(localToday.Year, localToday.Month, 1);

        var window = new TrainerDashboardWindow(
            localToday,
            today.StartUtc,
            today.EndUtc,
            nowUtc,
            nowUtc.AddHours(-DashboardThresholds.ReviewOverdueAfterHours),
            localToday.AddDays(DashboardThresholds.PackEndingWithinDays),
            localToday.AddDays(DashboardThresholds.PlanExpiringWithinDays),
            currentMonthStart,
            currentMonthStart.AddMonths(-1),
            currentMonthStart.AddMonths(1));

        var data = await _queries.GetAsync(actor.Value.TrainerId, window, cancellationToken);

        var withoutPlan = data.ClientsWithoutTrainingPlan
            .Select(row =>
            {
                var since = LocalDates.ToLocalDate(row.WithoutPlanSinceUtc, timeZone);
                return new TrainerDashboardDto.ClientWithoutPlanItemDto(
                    row.ClientId,
                    row.ClientName,
                    row.HasHadPlan,
                    since,
                    Math.Max(0, localToday.DayNumber - since.DayNumber));
            })
            .ToList();

        return Result<TrainerDashboardDto>.Success(new TrainerDashboardDto(
            localToday,
            data.ActiveClientCount,
            data.CheckInsPendingReview,
            data.PacksEnding,
            data.PlansExpiring,
            data.SessionsToday,
            new TrainerDashboardDto.ClientsWithoutTrainingPlanDto(
                data.ClientsWithoutTrainingPlanCount,
                withoutPlan),
            new TrainerDashboardDto.PackSalesDto(
                ToMonth(window.CurrentMonthStart, data.PackSales),
                ToMonth(window.PreviousMonthStart, data.PackSales))));
    }

    private static TrainerDashboardDto.PackSalesMonthDto ToMonth(
        DateOnly monthStart,
        IReadOnlyList<PackSalesRow> rows) =>
        new(
            monthStart.Year,
            monthStart.Month,
            rows.Where(row => row.MonthStart == monthStart)
                .OrderBy(row => row.Currency, StringComparer.Ordinal)
                .Select(row => new TrainerDashboardDto.PackSalesTotalDto(
                    row.Currency,
                    row.AmountCents,
                    row.PackCount))
                .ToList());
}
