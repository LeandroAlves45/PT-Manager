using Application.Common.Abstractions;
using Application.Features.Administration.ContentModeration;
using Application.Features.Administration.ContentModeration.Abstractions;
using Application.Features.Administration.ContentModeration.Dtos;
using Application.Features.Administration.ContentModeration.ListModerationQueue;
using Application.Features.Administration.Overview;
using Application.Features.Assessments.CheckIns.Abstractions;
using Application.Features.Assessments.CheckIns.Dtos;
using Application.Features.Assessments.CheckIns.GetMyNextCheckIn;
using Application.Features.Assessments.CheckIns.ListCheckIns;
using Application.Features.ClientPortal;
using Application.Features.ClientPortal.Abstractions;
using Application.Features.ClientPortal.Dtos;
using Application.Features.ClientPortal.GetMyPortalHome;
using Application.Features.ClientPortal.GetMyWorkoutToday;
using Application.Features.Clients;
using Application.Features.Clients.Abstractions;
using Application.Features.Clients.Dtos;
using Application.Features.Clients.GetClientSummary;
using Application.Features.Dashboard;
using Application.Features.Dashboard.Abstractions;
using Application.Features.Dashboard.Dtos;
using Application.Features.Dashboard.GetTrainerDashboard;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Pagination;

namespace Application.UnitTests.Features;

/// <summary>
/// Handler do dashboard: autorização por ator, resolução do dia local no fuso do trainer
/// e composição dos limiares e agregados.
/// </summary>
public sealed class GetTrainerDashboardHandlerTests : ReadHandlerTestContext
{
    [Fact]
    public async Task Dashboard_WithClientActor_IsForbidden()
    {
        var handler = new GetTrainerDashboardHandler(
            new TenantStub(TrainerId, ClientUserId, "client"),
            new ClockStub(NowUtc),
            new TimeZoneStub(Lisbon),
            new DashboardQueriesFake());

        var result = await handler.HandleAsync(new GetTrainerDashboardQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DashboardErrors.TrainerOnly.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Dashboard_UsesTrainerLocalDayAndThresholds()
    {
        var queries = new DashboardQueriesFake();
        var handler = new GetTrainerDashboardHandler(
            new TenantStub(TrainerId, TrainerId, "trainer"),
            new ClockStub(NowUtc),
            new TimeZoneStub(Lisbon),
            queries);

        var result = await handler.HandleAsync(new GetTrainerDashboardQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 9, 3), result.Value.LocalToday);
        Assert.Equal(new DateOnly(2026, 9, 10), queries.LastWindow!.PackEndingUntil);
        Assert.Equal(new DateOnly(2026, 9, 10), queries.LastWindow.PlanExpiringUntil);
        Assert.Equal(NowUtc.AddHours(-48), queries.LastWindow.ReviewOverdueBeforeUtc);
        Assert.Equal(new DateOnly(2026, 9, 1), queries.LastWindow.CurrentMonthStart);
        Assert.Equal(new DateOnly(2026, 8, 1), queries.LastWindow.PreviousMonthStart);
        Assert.Equal(new DateOnly(2026, 10, 1), queries.LastWindow.NextMonthStart);
    }

    [Fact]
    public async Task Dashboard_SplitsPackSalesByMonthAndCurrency()
    {
        var queries = new DashboardQueriesFake
        {
            PackSales =
            [
                new PackSalesRow(new DateOnly(2026, 9, 1), "EUR", 12_000, 2),
                new PackSalesRow(new DateOnly(2026, 8, 1), "EUR", 5_000, 1)
            ]
        };
        var handler = new GetTrainerDashboardHandler(
            new TenantStub(TrainerId, TrainerId, "trainer"),
            new ClockStub(NowUtc),
            new TimeZoneStub(Lisbon),
            queries);

        var result = await handler.HandleAsync(new GetTrainerDashboardQuery(), CancellationToken.None);

        var sales = result.Value.PackSales;
        Assert.Equal(12_000, Assert.Single(sales.CurrentMonth.Totals).AmountCents);
        Assert.Equal(5_000, Assert.Single(sales.PreviousMonth.Totals).AmountCents);
        Assert.Equal(9, sales.CurrentMonth.Month);
        Assert.Equal(8, sales.PreviousMonth.Month);
    }

    [Fact]
    public async Task Dashboard_ConvertsWithoutPlanSinceToLocalDays()
    {
        var queries = new DashboardQueriesFake
        {
            WithoutPlan =
            [
                new ClientWithoutPlanRow(
                    ClientId, "Rita Sá", false, new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc))
            ]
        };
        var handler = new GetTrainerDashboardHandler(
            new TenantStub(TrainerId, TrainerId, "trainer"),
            new ClockStub(NowUtc),
            new TimeZoneStub(Lisbon),
            queries);

        var result = await handler.HandleAsync(new GetTrainerDashboardQuery(), CancellationToken.None);

        var item = Assert.Single(result.Value.ClientsWithoutTrainingPlan.Items);
        Assert.Equal(new DateOnly(2026, 8, 25), item.WithoutPlanSince);
        Assert.Equal(9, item.DaysWithoutPlan);
        Assert.False(item.HasHadPlan);
    }

}
