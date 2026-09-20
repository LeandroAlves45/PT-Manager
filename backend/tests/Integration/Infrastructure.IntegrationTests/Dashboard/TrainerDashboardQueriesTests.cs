using System.Data.Common;
using Application.Features.Administration.ContentModeration.ListModerationQueue;
using Application.Features.Assessments.CheckIns.ListCheckIns;
using Application.Features.Clients.ListClients;
using Application.Features.Dashboard;
using Application.Features.Dashboard.Abstractions;
using Application.Features.Nutrition.MealPlans.ListMealPlans;
using Application.Features.Training.TrainingPlans.ListTrainingPlans;
using Application.Pagination;
using Domain.Entities.Assessments;
using Domain.Entities.Billing;
using Domain.Entities.Nutrition;
using Domain.Entities.Sessions;
using Domain.Entities.Training;
using Domain.Services;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.Data.Interceptors;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Administration;
using Infrastructure.Persistence.Assessments;
using Infrastructure.Persistence.ClientPortal;
using Infrastructure.Persistence.Clients;
using Infrastructure.Persistence.Dashboard;
using Infrastructure.Persistence.Nutrition;
using Infrastructure.Persistence.Training;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.IntegrationTests.Dashboard;

/// <summary>
/// Queries do dashboard contra PostgreSQL real: contagens exatas acima de 100 registos,
/// isolamento entre trainers e orçamento fixo de comandos.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class TrainerDashboardQueriesTests : ReadQueriesIntegrationTestContext
{
    public TrainerDashboardQueriesTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Dashboard_CountsEveryPendingReviewAboveTopN()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);
        await SeedPendingReviewCheckInsAsync(seed, 120, token);

        var dashboard = await ReadDashboardAsync(seed.TrainerId, token);

        Assert.Equal(120, dashboard.CheckInsPendingReview.TotalCount);
        Assert.Equal(DashboardThresholds.TopCount, dashboard.CheckInsPendingReview.Items.Count);
        // Todos os 120 foram respondidos há mais de 48 h.
        Assert.Equal(120, dashboard.CheckInsPendingReview.OverdueCount);
        // O primeiro item é o mais antigo por RespondedAt.
        Assert.Equal(
            dashboard.CheckInsPendingReview.Items.Min(item => item.RespondedAt),
            dashboard.CheckInsPendingReview.Items[0].RespondedAt);
    }

    [Fact]
    public async Task Dashboard_IgnoresAnotherTrainerData()
    {
        var token = TestContext.Current.CancellationToken;
        var owner = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);
        var other = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);
        await SeedPendingReviewCheckInsAsync(owner, 3, token);
        await SeedPendingReviewCheckInsAsync(other, 7, token);

        var dashboard = await ReadDashboardAsync(owner.TrainerId, token);

        Assert.Equal(3, dashboard.CheckInsPendingReview.TotalCount);
        Assert.Equal(1, dashboard.ActiveClientCount);
    }

    [Fact]
    public async Task Dashboard_UsesFixedCommandBudgetRegardlessOfVolume()
    {
        var token = TestContext.Current.CancellationToken;
        var empty = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);
        var loaded = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);
        await SeedPendingReviewCheckInsAsync(loaded, 120, token);

        var emptyCounter = new CommandCounter();
        await using (var context = CreateMeasuredContext(empty.TrainerId, emptyCounter))
        {
            await new TrainerDashboardQueries(context)
                .GetAsync(empty.TrainerId, Window(), token);
        }

        var loadedCounter = new CommandCounter();
        await using (var context = CreateMeasuredContext(loaded.TrainerId, loadedCounter))
        {
            await new TrainerDashboardQueries(context)
                .GetAsync(loaded.TrainerId, Window(), token);
        }

        Assert.Equal(emptyCounter.ReaderCommands, loadedCounter.ReaderCommands);
        Assert.True(
            loadedCounter.ReaderCommands <= 14,
            $"Dashboard used {loadedCounter.ReaderCommands} commands (budget 14).");
    }

    [Fact]
    public async Task Dashboard_SummarisesPacksSessionsAndSales()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);

        await using (var context = _fixture.CreateContext(seed.TrainerId))
        {
            var packType = new PackType(seed.TrainerId, "Pack 10", 10, 25_000, "EUR", 60, Now);
            var currentMonthPack = new ClientSessionPack(
                seed.TrainerId, seed.ClientId, packType, new DateOnly(2026, 9, 1), LocalToday.AddDays(3), Now);
            var previousMonthPack = new ClientSessionPack(
                seed.TrainerId, seed.ClientId, packType, new DateOnly(2026, 8, 12), null, Now);

            context.PackTypes.Add(packType);
            context.ClientSessionPacks.AddRange(currentMonthPack, previousMonthPack);
            context.Sessions.Add(new Session(
                seed.TrainerId,
                seed.ClientId,
                null,
                new DateTimeOffset(2026, 9, 3, 16, 0, 0, TimeSpan.Zero),
                60,
                "Estúdio A",
                "PT individual",
                null,
                Now));
            await context.SaveChangesAsync(token);
        }

        var dashboard = await ReadDashboardAsync(seed.TrainerId, token);

        // O pack do mês corrente tem fim previsto dentro de 7 dias: conta como "a terminar".
        Assert.Equal(1, dashboard.PacksEnding.TotalCount);
        Assert.Equal(10, dashboard.PacksEnding.Items[0].SessionsTotal);
        Assert.Equal(1, dashboard.SessionsToday.TotalCount);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 3, 16, 0, 0, TimeSpan.Zero),
            dashboard.SessionsToday.NextSessionStartsAt);
        var currentMonth = Assert.Single(
            dashboard.PackSales, row => row.MonthStart == new DateOnly(2026, 9, 1));
        var previousMonth = Assert.Single(
            dashboard.PackSales, row => row.MonthStart == new DateOnly(2026, 8, 1));
        Assert.Equal(25_000, currentMonth.AmountCents);
        Assert.Equal(25_000, previousMonth.AmountCents);
        Assert.Equal("EUR", currentMonth.Currency);
    }

    [Fact]
    public async Task Dashboard_ListsExpiringPlansAndClientsWithoutPlan()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);

        await using (var context = _fixture.CreateContext(seed.TrainerId))
        {
            var plan = new TrainingPlan(
                seed.TrainerId, seed.ClientId, "Força", null, null, null,
                new DateOnly(2026, 8, 31), LocalToday.AddDays(4), Now);
            context.TrainingPlans.Add(plan);
            context.MealPlans.Add(MealPlanFor(seed, LocalToday.AddDays(2)));
            await context.SaveChangesAsync(token);
        }

        var withPlan = await ReadDashboardAsync(seed.TrainerId, token);

        Assert.Equal(2, withPlan.PlansExpiring.TotalCount);
        Assert.Equal(1, withPlan.PlansExpiring.TrainingPlanCount);
        Assert.Equal(1, withPlan.PlansExpiring.MealPlanCount);
        Assert.Equal(0, withPlan.ClientsWithoutTrainingPlanCount);

        await using (var context = _fixture.CreateContext(seed.TrainerId))
        {
            var plan = await context.TrainingPlans.SingleAsync(item => item.ClientId == seed.ClientId, token);
            plan.Archive(Now);
            await context.SaveChangesAsync(token);
        }

        var archived = await ReadDashboardAsync(seed.TrainerId, token);

        Assert.Equal(1, archived.ClientsWithoutTrainingPlanCount);
        var item = Assert.Single(archived.ClientsWithoutTrainingPlan);
        Assert.True(item.HasHadPlan);
        Assert.Equal(0, archived.PlansExpiring.TrainingPlanCount);
    }

}
