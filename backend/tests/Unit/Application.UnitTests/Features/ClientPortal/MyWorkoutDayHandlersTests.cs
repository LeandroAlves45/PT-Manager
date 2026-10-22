using Application.Features.ClientPortal;
using Application.Features.ClientPortal.Abstractions;
using Application.Features.ClientPortal.Dtos;
using Application.Features.ClientPortal.GetMyWorkoutDay;
using Application.Features.ClientPortal.GetMyWorkoutToday;
using Application.Features.ClientPortal.ListMyWorkoutDays;

namespace Application.UnitTests.Features;

/// <summary>
/// Filtro de dia do portal. O relógio está em 2026-09-02 23:30 UTC, que já é 3 de setembro em
/// Lisboa: os testes provam que "hoje" e "ontem" são os do fuso do personal trainer. O plano
/// começa a 31 de agosto (segunda) e repete todas as semanas.
/// </summary>
public sealed class MyWorkoutDayHandlersTests : ReadHandlerTestContext
{
    private static ListMyWorkoutDaysHandler DaysHandler(int dayOfWeek) =>
        new(
            new TenantStub(TrainerId, ClientUserId, "client"),
            new ClockStub(NowUtc),
            new TimeZoneStub(Lisbon),
            new TrainingPlanQueriesFake { Plan = PlanWith(dayOfWeek) });

    private static GetMyWorkoutDayHandler DayHandler(
        int dayOfWeek,
        WorkoutTodayQueriesFake? queries = null) =>
        new(
            new TenantStub(TrainerId, ClientUserId, "client"),
            new ClockStub(NowUtc),
            new TimeZoneStub(Lisbon),
            new MyWorkoutTodayReader(
                new TrainingPlanQueriesFake { Plan = PlanWith(dayOfWeek) },
                queries ?? new WorkoutTodayQueriesFake()));

    [Fact]
    public async Task Days_ListsYesterdayInTrainerTimeZone()
    {
        // Treino à quarta: 2 de setembro já é ontem em Lisboa, embora ainda seja hoje em UTC.
        var result = await DaysHandler(dayOfWeek: 2)
            .HandleAsync(new ListMyWorkoutDaysQuery(), CancellationToken.None);

        var day = Assert.Single(result.Value);
        Assert.Equal(new DateOnly(2026, 9, 2), day.Date);
        Assert.Equal(1, day.WeekNumber);
        Assert.Equal(2, day.DayOfWeek);
    }

    [Fact]
    public async Task Days_NeverListsToday()
    {
        // Treino à quinta: hoje (3 de setembro) é o primeiro e ainda não passou.
        var result = await DaysHandler(dayOfWeek: 3)
            .HandleAsync(new ListMyWorkoutDaysQuery(), CancellationToken.None);

        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Days_WithoutActivePlan_ReturnsNotFound()
    {
        var handler = new ListMyWorkoutDaysHandler(
            new TenantStub(TrainerId, ClientUserId, "client"),
            new ClockStub(NowUtc),
            new TimeZoneStub(Lisbon),
            new TrainingPlanQueriesFake());

        var result = await handler.HandleAsync(new ListMyWorkoutDaysQuery(), CancellationToken.None);

        Assert.Equal(ClientPortalErrors.TrainingPlanNotAvailable.Code, result.Error!.Code);
    }

    [Theory]
    [InlineData(2026, 9, 3)] // hoje, em Lisboa
    [InlineData(2026, 9, 10)] // futuro
    [InlineData(2026, 8, 30)] // antes do início do plano
    public async Task Day_TodayFutureOrOutsidePlan_IsRejected(int year, int month, int day)
    {
        var result = await DayHandler(dayOfWeek: 0).HandleAsync(
            new GetMyWorkoutDayQuery(new DateOnly(year, month, day)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ClientPortalErrors.WorkoutDayNotAvailable.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Day_WithoutDate_IsRejected()
    {
        var result = await DayHandler(dayOfWeek: 0)
            .HandleAsync(new GetMyWorkoutDayQuery(null), CancellationToken.None);

        Assert.Equal(ClientPortalErrors.WorkoutDayNotAvailable.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Day_PastWorkoutDay_ReturnsLogsNotesAndCompletionOfThatDay()
    {
        var queries = new WorkoutTodayQueriesFake
        {
            Logs =
            [
                new MyTodaySetLogRow(
                    Guid.NewGuid(),
                    PrescriptionId,
                    1,
                    60m,
                    10,
                    null,
                    new DateTimeOffset(2026, 9, 2, 9, 0, 0, TimeSpan.Zero),
                    "Pesado")
            ],
            CompletedAt = new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc),
            CompletionNotes = "Correu bem"
        };

        var result = await DayHandler(dayOfWeek: 2, queries).HandleAsync(
            new GetMyWorkoutDayQuery(new DateOnly(2026, 9, 2)),
            CancellationToken.None);

        Assert.Equal(MyWorkoutTodayStatus.Workout, result.Value.Status);
        Assert.Equal(new DateOnly(2026, 9, 2), result.Value.LocalDate);
        Assert.Equal("Correu bem", result.Value.CompletionNotes);
        Assert.Null(result.Value.NextWorkout);
        var set = result.Value.Day!.Exercises[0].Sets[0];
        Assert.Equal("Pesado", set.Logged!.Notes);
        // A janela dos registos é o dia local pedido, em Lisboa, convertido para UTC.
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 23, 0, 0, TimeSpan.Zero), queries.LastFromUtc);
    }

    [Fact]
    public async Task Day_PastRestDay_ReturnsRestWithoutDay()
    {
        var result = await DayHandler(dayOfWeek: 2).HandleAsync(
            new GetMyWorkoutDayQuery(new DateOnly(2026, 9, 1)),
            CancellationToken.None);

        Assert.Equal(MyWorkoutTodayStatus.Rest, result.Value.Status);
        Assert.Null(result.Value.Day);
    }

    [Fact]
    public async Task Day_TrainerActor_IsForbidden()
    {
        var handler = new GetMyWorkoutDayHandler(
            new TenantStub(TrainerId, TrainerId, "trainer"),
            new ClockStub(NowUtc),
            new TimeZoneStub(Lisbon),
            new MyWorkoutTodayReader(new TrainingPlanQueriesFake(), new WorkoutTodayQueriesFake()));

        var result = await handler.HandleAsync(
            new GetMyWorkoutDayQuery(new DateOnly(2026, 9, 2)),
            CancellationToken.None);

        Assert.Equal(ClientPortalErrors.ClientOnly.Code, result.Error!.Code);
    }
}
