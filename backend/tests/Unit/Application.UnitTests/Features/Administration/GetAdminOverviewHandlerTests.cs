using Application.Features.Administration.Overview;
using Application.UnitTests.Features.Authentication;

namespace Application.UnitTests.Features;

public sealed class GetAdminOverviewHandlerTests : ReadHandlerTestContext
{
    private static readonly TestClock Clock = new(new DateTime(2026, 12, 15, 10, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task AdminOverview_WithTrainerActor_IsForbidden()
    {
        var handler = new GetAdminOverviewHandler(
            new TenantStub(TrainerId, TrainerId, "trainer"), new AdminOverviewQueriesFake(), Clock);

        var result = await handler.HandleAsync(new GetAdminOverviewQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AdminOverviewErrors.AdministratorOnly.Code, result.Error!.Code);
    }

    [Fact]
    public async Task AdminOverview_WithAdministrator_ReturnsCounts()
    {
        var handler = new GetAdminOverviewHandler(
            new AdministrativeTenantStub(), new AdminOverviewQueriesFake(), Clock);

        var result = await handler.HandleAsync(new GetAdminOverviewQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.GlobalFoods.TotalCount);
        Assert.Equal(1, result.Value.PrivateExercises.BlockedCount);
        Assert.Equal(2789, result.Value.Trainers.RevenueThisMonthCents);
    }

    [Fact]
    public async Task AdminOverview_InWinter_UsesTheLisbonCalendarMonth()
    {
        var queries = new AdminOverviewQueriesFake();
        var handler = new GetAdminOverviewHandler(new AdministrativeTenantStub(), queries, Clock);

        await handler.HandleAsync(new GetAdminOverviewQuery(), CancellationToken.None);

        Assert.Equal(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero), queries.LastRevenuePeriod!.Value.StartUtc);
        Assert.Equal(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero), queries.LastRevenuePeriod!.Value.EndUtc);
    }

    [Fact]
    public async Task AdminOverview_LateOnTheLastSummerDayInUtc_IsAlreadyNextMonthInLisbon()
    {
        // 23:30 UTC de 31 de julho é 00:30 de 1 de agosto em Lisboa (UTC+1).
        var queries = new AdminOverviewQueriesFake();
        var handler = new GetAdminOverviewHandler(
            new AdministrativeTenantStub(),
            queries,
            new TestClock(new DateTime(2026, 7, 31, 23, 30, 0, DateTimeKind.Utc)));

        await handler.HandleAsync(new GetAdminOverviewQuery(), CancellationToken.None);

        Assert.Equal(new DateTimeOffset(2026, 7, 31, 23, 0, 0, TimeSpan.Zero), queries.LastRevenuePeriod!.Value.StartUtc);
        Assert.Equal(new DateTimeOffset(2026, 8, 31, 23, 0, 0, TimeSpan.Zero), queries.LastRevenuePeriod!.Value.EndUtc);
    }
}
