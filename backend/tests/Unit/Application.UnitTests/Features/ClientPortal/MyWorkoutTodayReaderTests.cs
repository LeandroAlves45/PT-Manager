using Application.Features.ClientPortal;
using Application.Features.ClientPortal.Abstractions;
using Application.Features.ClientPortal.Dtos;
using Application.Features.ClientPortal.GetMyWorkoutToday;

namespace Application.UnitTests.Features;

public sealed class MyWorkoutTodayReaderTests : ReadHandlerTestContext
{
    [Fact]
    public async Task WorkoutToday_WithoutActivePlan_ReturnsNotFound()
    {
        var handler = new GetMyWorkoutTodayHandler(
            new TenantStub(TrainerId, ClientUserId, "client"),
            new ClockStub(NowUtc),
            new TimeZoneStub(Lisbon),
            new MyWorkoutTodayReader(new TrainingPlanQueriesFake(), new WorkoutTodayQueriesFake()));

        var result = await handler.HandleAsync(new GetMyWorkoutTodayQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ClientPortalErrors.TrainingPlanNotAvailable.Code, result.Error!.Code);
    }

    [Fact]
    public async Task WorkoutToday_OnRestDay_ReturnsNextWorkout()
    {
        // Plano de uma semana só com treino à segunda; hoje (3/9/2026) é quinta.
        var reader = new MyWorkoutTodayReader(
            new TrainingPlanQueriesFake { Plan = PlanWith(dayOfWeek: 0) },
            new WorkoutTodayQueriesFake());
        var handler = new GetMyWorkoutTodayHandler(
            new TenantStub(TrainerId, ClientUserId, "client"),
            new ClockStub(NowUtc),
            new TimeZoneStub(Lisbon),
            reader);

        var result = await handler.HandleAsync(new GetMyWorkoutTodayQuery(), CancellationToken.None);

        Assert.Equal(MyWorkoutTodayStatus.Rest, result.Value.Status);
        Assert.Null(result.Value.Day);
        Assert.Equal(new DateOnly(2026, 9, 7), result.Value.NextWorkout!.Date);
    }

    [Fact]
    public async Task WorkoutToday_OnWorkoutDay_MergesLogsAndCompletion()
    {
        var queries = new WorkoutTodayQueriesFake
        {
            Logs =
            [
                new MyTodaySetLogRow(
                    Guid.NewGuid(),
                    PrescriptionId,
                    1,
                    62.5m,
                    8,
                    8m,
                    new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero))
            ],
            CompletedAt = new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc)
        };
        var reader = new MyWorkoutTodayReader(
            new TrainingPlanQueriesFake { Plan = PlanWith(dayOfWeek: 3) },
            queries);
        var handler = new GetMyWorkoutTodayHandler(
            new TenantStub(TrainerId, ClientUserId, "client"),
            new ClockStub(NowUtc),
            new TimeZoneStub(Lisbon),
            reader);

        var result = await handler.HandleAsync(new GetMyWorkoutTodayQuery(), CancellationToken.None);

        Assert.Equal(MyWorkoutTodayStatus.Workout, result.Value.Status);
        Assert.Equal(new DateOnly(2026, 9, 3), result.Value.LocalDate);
        Assert.Equal(2, result.Value.Progress.PlannedSets);
        Assert.Equal(1, result.Value.Progress.LoggedSets);
        Assert.Equal(1, result.Value.Progress.PlannedExercises);
        Assert.Equal(0, result.Value.Progress.CompletedExercises);
        Assert.NotNull(result.Value.CompletedAt);
        var sets = Assert.Single(result.Value.Day!.Exercises).Sets;
        Assert.NotNull(sets[0].Logged);
        Assert.Null(sets[1].Logged);
        // A janela de registos é o dia local, convertido para UTC.
        Assert.Equal(
            new DateTimeOffset(2026, 9, 2, 23, 0, 0, TimeSpan.Zero), queries.LastFromUtc);
    }

    [Fact]
    public async Task WorkoutToday_OutsidePlanDates_ReportsOutsidePlan()
    {
        var plan = PlanWith(dayOfWeek: 3) with
        {
            StartDate = new DateOnly(2026, 8, 1),
            EndDate = new DateOnly(2026, 8, 31)
        };
        var reader = new MyWorkoutTodayReader(
            new TrainingPlanQueriesFake { Plan = plan },
            new WorkoutTodayQueriesFake());
        var handler = new GetMyWorkoutTodayHandler(
            new TenantStub(TrainerId, ClientUserId, "client"),
            new ClockStub(NowUtc),
            new TimeZoneStub(Lisbon),
            reader);

        var result = await handler.HandleAsync(new GetMyWorkoutTodayQuery(), CancellationToken.None);

        Assert.Equal(MyWorkoutTodayStatus.OutsidePlan, result.Value.Status);
        Assert.Null(result.Value.NextWorkout);
    }

}
