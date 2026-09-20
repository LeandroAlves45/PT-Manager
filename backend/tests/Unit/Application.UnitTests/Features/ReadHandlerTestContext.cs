using Application.Common.Abstractions;
using Application.Features.Administration.ContentModeration.Abstractions;
using Application.Features.Administration.ContentModeration.Dtos;
using Application.Features.Administration.Overview;
using Application.Features.Assessments.CheckIns.Abstractions;
using Application.Features.Assessments.CheckIns.Dtos;
using Application.Features.ClientPortal.Abstractions;
using Application.Features.ClientPortal.Dtos;
using Application.Features.ClientPortal.GetMyPortalHome;
using Application.Features.ClientPortal.GetMyWorkoutToday;
using Application.Features.Clients.Abstractions;
using Application.Features.Clients.Dtos;
using Application.Features.Dashboard.Abstractions;
using Application.Features.Dashboard.Dtos;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Features.Administration.ContentModeration.ListModerationQueue;
using Application.Features.Assessments.CheckIns.ListCheckIns;
using Application.Pagination;

namespace Application.UnitTests.Features;

public abstract class ReadHandlerTestContext
{
    protected static readonly Guid TrainerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid ClientUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    protected static readonly Guid ClientId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    protected static readonly Guid PrescriptionId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    // A fronteira UTC prova que os handlers resolvem o dia no fuso do trainer.
    protected static readonly DateTime NowUtc = new(2026, 9, 2, 23, 30, 0, DateTimeKind.Utc);
    protected static readonly TimeZoneInfo Lisbon = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    protected static GetMyPortalHomeHandler CreateHomeHandler(
        IntakeQueriesFake intakes,
        MyNextCheckInDto? nextCheckIn = null) =>
        new(
            new TenantStub(TrainerId, ClientUserId, "client"),
            new ClockStub(NowUtc),
            new TimeZoneStub(Lisbon),
            new MyWorkoutTodayReader(
                new TrainingPlanQueriesFake { Plan = PlanWith(dayOfWeek: 3) },
                new WorkoutTodayQueriesFake()),
            new NutritionQueriesFake(),
            intakes,
            new CheckInQueriesFake { Next = nextCheckIn });

    protected static MyTrainingPlanDto PlanWith(int dayOfWeek) =>
        new(
            Guid.NewGuid(),
            "Força 3x",
            null,
            null,
            null,
            new DateOnly(2026, 8, 31),
            null,
            [
                new MyTrainingPlanDto.DayDto(
                    Guid.NewGuid(),
                    dayOfWeek,
                    1,
                    "Empurrar",
                    [
                        new MyTrainingPlanDto.ExerciseDto(
                            PrescriptionId,
                            1,
                            "Supino",
                            false,
                            null,
                            null,
                            null,
                            [
                                new MyTrainingPlanDto.SetDto(Guid.NewGuid(), 1, 8, 60m, 60, 90, 8m),
                                new MyTrainingPlanDto.SetDto(Guid.NewGuid(), 2, 8, 60m, 60, 90, 8m)
                            ])
                    ])
            ],
            new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc));

    protected sealed class TenantStub(Guid trainerId, Guid userId, string role) : ITenantContext
    {
        public Guid? TrainerId { get; } = trainerId;
        public Guid? UserId { get; } = userId;
        public string? Role { get; } = role;
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative => false;
    }

    protected sealed class AdministrativeTenantStub : ITenantContext
    {
        public Guid? TrainerId => null;
        public Guid? UserId { get; } = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        public string? Role => "superuser";
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative => true;
    }

    protected sealed class ClockStub(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    protected sealed class TimeZoneStub(TimeZoneInfo timeZone) : ITrainerTimeZoneProvider
    {
        public Task<TimeZoneInfo> GetRequiredAsync(
            Guid trainerId, CancellationToken cancellationToken) => Task.FromResult(timeZone);
    }

    protected sealed class DashboardQueriesFake : ITrainerDashboardQueries
    {
        public TrainerDashboardWindow? LastWindow { get; private set; }

        public IReadOnlyList<PackSalesRow> PackSales { get; init; } = [];

        public IReadOnlyList<ClientWithoutPlanRow> WithoutPlan { get; init; } = [];

        public Task<TrainerDashboardData> GetAsync(
            Guid trainerId,
            TrainerDashboardWindow window,
            CancellationToken cancellationToken)
        {
            LastWindow = window;
            return Task.FromResult(new TrainerDashboardData(
                4,
                new TrainerDashboardDto.CheckInsPendingReviewDto(7, 3, []),
                new TrainerDashboardDto.PacksEndingDto(2, []),
                new TrainerDashboardDto.PlansExpiringDto(4, 3, 1, [], []),
                new TrainerDashboardDto.SessionsTodayDto(5, null, []),
                WithoutPlan.Count,
                WithoutPlan,
                PackSales));
        }
    }

    protected sealed class ClientSummaryQueriesFake : IClientProgressSummaryQueries
    {
        public ClientProgressSnapshot? Snapshot { get; init; }

        public DateOnly LastWeightWindowStart { get; private set; }

        public Task<ClientProgressSnapshot?> GetAsync(
            Guid clientId,
            DateOnly weightWindowStart,
            DateTimeOffset logsFromUtc,
            DateTimeOffset logsToUtc,
            CancellationToken cancellationToken)
        {
            LastWeightWindowStart = weightWindowStart;
            return Task.FromResult(Snapshot);
        }
    }

    protected sealed class TrainingPlanQueriesFake : IMyTrainingPlanQueries
    {
        public MyTrainingPlanDto? Plan { get; init; }

        public Task<MyTrainingPlanDto?> GetActiveAsync(
            Guid trainerId, Guid clientUserId, CancellationToken cancellationToken) =>
            Task.FromResult(Plan);
    }

    protected sealed class WorkoutTodayQueriesFake : IMyWorkoutTodayQueries
    {
        public IReadOnlyList<MyTodaySetLogRow> Logs { get; init; } = [];

        public DateTime? CompletedAt { get; init; }

        public DateTimeOffset LastFromUtc { get; private set; }

        public Task<IReadOnlyList<MyTodaySetLogRow>> ListMyLogsAsync(
            Guid trainerId,
            Guid clientUserId,
            Guid trainingPlanId,
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken)
        {
            LastFromUtc = fromUtc;
            return Task.FromResult(Logs);
        }

        public Task<DateTime?> GetMyCompletionAsync(
            Guid trainerId,
            Guid clientUserId,
            Guid trainingPlanDayId,
            DateOnly localDate,
            CancellationToken cancellationToken) => Task.FromResult(CompletedAt);
    }

    protected sealed class NutritionQueriesFake : IMyNutritionPlanQueries
    {
        public Task<MyNutritionPlanDto?> GetActiveAsync(
            Guid trainerId, Guid clientUserId, CancellationToken cancellationToken) =>
            Task.FromResult<MyNutritionPlanDto?>(new MyNutritionPlanDto(
                Guid.NewGuid(),
                "Recomposição",
                null,
                new DateOnly(2026, 9, 1),
                null,
                2_200m,
                180m,
                200m,
                60m,
                new MyNutritionPlanDto.TotalsDto(0, 0, 0, 0, 0),
                [
                    new MyNutritionPlanDto.MealDto(
                        "breakfast",
                        1,
                        new MyNutritionPlanDto.TotalsDto(0, 0, 0, 0, 0),
                        [],
                        [])
                ],
                new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    protected sealed class IntakeQueriesFake : ISupplementIntakeQueries
    {
        public MyTodaySupplementIntakesDto? Result { get; init; }

        public Task<MyTodaySupplementIntakesDto?> ListMyForDateAsync(
            Guid trainerId,
            Guid clientUserId,
            DateOnly localDate,
            CancellationToken cancellationToken) => Task.FromResult(Result);
    }

    protected sealed class CheckInQueriesFake : ICheckInQueries
    {
        public MyNextCheckInDto? Next { get; init; }

        public DateOnly LastLocalToday { get; private set; }

        public Task<CheckInDto?> GetAsync(
            Guid trainerId, Guid checkInId, DateOnly localToday, CancellationToken cancellationToken) =>
            Task.FromResult<CheckInDto?>(null);

        public Task<PageResult<CheckInDto>> ListAsync(
            Guid trainerId,
            Guid? clientId,
            CheckInStatusFilter? status,
            DateOnly? fromDate,
            DateOnly? toDate,
            DateOnly localToday,
            PageRequest page,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PageResult<CheckInDto>([], 0));

        public Task<CheckInDto?> GetMyDueAsync(
            Guid trainerId, Guid userId, DateOnly localToday, CancellationToken cancellationToken) =>
            Task.FromResult<CheckInDto?>(null);

        public Task<MyNextCheckInDto?> GetMyNextAsync(
            Guid trainerId, Guid userId, DateOnly localToday, CancellationToken cancellationToken)
        {
            LastLocalToday = localToday;
            return Task.FromResult(Next);
        }
    }

    protected sealed class ModerationQueueQueriesFake : IModerationQueueQueries
    {
        public int Calls { get; private set; }

        public string? LastSearch { get; private set; }

        public ModerationStatusFilter LastStatus { get; private set; }

        public ModerationContentKind LastKind { get; private set; }

        public Task<PageResult<ModerationQueueItemDto>> ListAsync(
            ModerationContentKind kind,
            ModerationStatusFilter status,
            string? search,
            PageRequest page,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastKind = kind;
            LastStatus = status;
            LastSearch = search;
            return Task.FromResult(new PageResult<ModerationQueueItemDto>([], 0));
        }
    }

    protected sealed class AdminOverviewQueriesFake : IAdminOverviewQueries
    {
        public Task<AdminOverviewDto> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new AdminOverviewDto(
                new AdminOverviewDto.CatalogCountsDto(3, 2, 1),
                new AdminOverviewDto.CatalogCountsDto(0, 0, 0),
                new AdminOverviewDto.CatalogCountsDto(0, 0, 0),
                new AdminOverviewDto.ModerationCountsDto(5, 0),
                new AdminOverviewDto.ModerationCountsDto(4, 1)));
    }
}
