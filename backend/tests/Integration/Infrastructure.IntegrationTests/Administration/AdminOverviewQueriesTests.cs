using Application.Common.Time;
using Domain.Entities.Billing;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Administration;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Administration;

[Collection(PostgresCollection.Name)]
public sealed class AdminOverviewQueriesTests : ReadQueriesIntegrationTestContext
{
    // Um mês que nenhum outro teste usa: o dinheiro da suite partilhada não se mistura.
    private static readonly UtcRange IsolatedMonth = new(
        new DateTimeOffset(2031, 2, 28, 23, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2031, 3, 31, 22, 0, 0, TimeSpan.Zero));

    public AdminOverviewQueriesTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task AdminOverview_SeparatesGlobalAndPrivateCounts()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);
        var marker = NewDiscriminator();
        await SeedPrivateFoodAsync(seed.TrainerId, $"overview-{marker}", token);
        await SeedGlobalFoodAsync($"overview-{marker}-global", token);

        await using var context = _fixture.CreateAdministrativeContext();
        var first = await new AdminOverviewQueries(context).GetAsync(IsolatedMonth, token);

        await SeedPrivateFoodAsync(seed.TrainerId, $"overview-{marker}-2", token);

        await using var secondContext = _fixture.CreateAdministrativeContext();
        var second = await new AdminOverviewQueries(secondContext).GetAsync(IsolatedMonth, token);

        Assert.Equal(first.PrivateFoods.TotalCount + 1, second.PrivateFoods.TotalCount);
        Assert.Equal(first.GlobalFoods.TotalCount, second.GlobalFoods.TotalCount);
        Assert.True(second.GlobalFoods.TotalCount >= 1);
    }

    [Fact]
    public async Task TrainerCounts_FollowRn018Overlaps()
    {
        var token = TestContext.Current.CancellationToken;
        var before = await ReadAsync(token);

        // Ativo (no trial), suspenso, isento (fica fora de ativo), cancelado e apagado não contam.
        await SeedTrainerAsync(subscription => { }, deleteUser: false, token);
        await SeedTrainerAsync(subscription => subscription.Suspend(Now), deleteUser: false, token);
        await SeedTrainerAsync(subscription => subscription.SetBillingExemption(true, Now), deleteUser: false, token);
        await SeedTrainerAsync(subscription => subscription.Cancel(Now), deleteUser: false, token);
        await SeedTrainerAsync(subscription => { }, deleteUser: true, token);

        var after = await ReadAsync(token);

        Assert.Equal(before.Trainers.ActiveSubscriptionCount + 1, after.Trainers.ActiveSubscriptionCount);
        Assert.Equal(before.Trainers.SuspendedSubscriptionCount + 1, after.Trainers.SuspendedSubscriptionCount);
        Assert.Equal(before.Trainers.ExemptCount + 1, after.Trainers.ExemptCount);
    }

    [Fact]
    public async Task Revenue_SumsNetEurosPaidInsideTheHalfOpenMonth()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);
        var before = (await ReadAsync(token)).Trainers.RevenueThisMonthCents;

        await using (var context = _fixture.CreateAdministrativeContext())
        {
            context.PlatformSubscriptionPayments.AddRange(
                Payment(seed.TrainerId, 2900, 2789, "eur", IsolatedMonth.StartUtc.UtcDateTime),
                Payment(seed.TrainerId, 2900, 2789, "eur", IsolatedMonth.EndUtc.UtcDateTime.AddSeconds(-1)),
                Payment(seed.TrainerId, 2900, 2789, "eur", IsolatedMonth.EndUtc.UtcDateTime),
                Payment(seed.TrainerId, 2900, 2789, "eur", IsolatedMonth.StartUtc.UtcDateTime.AddSeconds(-1)),
                Payment(seed.TrainerId, 2900, 2789, "usd", IsolatedMonth.StartUtc.UtcDateTime.AddDays(1)));
            await context.SaveChangesAsync(token);
        }

        var after = (await ReadAsync(token)).Trainers.RevenueThisMonthCents;

        Assert.Equal(before + 2789 * 2, after);
    }

    private async Task<Application.Features.Administration.Overview.AdminOverviewDto> ReadAsync(
        CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateAdministrativeContext();
        return await new AdminOverviewQueries(context).GetAsync(IsolatedMonth, cancellationToken);
    }

    private async Task SeedTrainerAsync(
        Action<TrainerSubscription> arrange,
        bool deleteUser,
        CancellationToken cancellationToken)
    {
        var seed = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), cancellationToken);
        var subscription = new TrainerSubscription(seed.TrainerId, Now.AddDays(14), Now);
        arrange(subscription);

        await using (var context = _fixture.CreateContext(seed.TrainerId))
        {
            context.TrainerSubscriptions.Add(subscription);
            await context.SaveChangesAsync(cancellationToken);
        }

        if (deleteUser)
        {
            await using var context = _fixture.CreateAdministrativeContext();
            await context.Users.IgnoreQueryFilters()
                .Where(user => user.Id == seed.TrainerId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.IsDeleted, true), cancellationToken);
        }
    }

    private static PlatformSubscriptionPayment Payment(
        Guid trainerId,
        long amount,
        long net,
        string currency,
        DateTime paidAt) =>
        new(trainerId, $"in_{Guid.NewGuid():N}", amount, net, currency, paidAt, Now);
}
