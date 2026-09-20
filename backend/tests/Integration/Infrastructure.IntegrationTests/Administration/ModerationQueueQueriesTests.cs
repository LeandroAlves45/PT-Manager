using Application.Features.Administration.ContentModeration.ListModerationQueue;
using Application.Pagination;
using Domain.Entities.Nutrition;
using Domain.ValueObjects;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Administration;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Administration;

[Collection(PostgresCollection.Name)]
public sealed class ModerationQueueQueriesTests : ReadQueriesIntegrationTestContext
{
    public ModerationQueueQueriesTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ModerationQueue_ReturnsPrivateContentOfEveryTrainer()
    {
        var token = TestContext.Current.CancellationToken;
        var first = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);
        var second = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);
        var marker = NewDiscriminator();

        await SeedPrivateFoodAsync(first.TrainerId, $"queue-{marker}-a", token);
        await SeedPrivateFoodAsync(second.TrainerId, $"queue-{marker}-b", token);
        await SeedGlobalFoodAsync($"queue-{marker}-global", token);

        await using var context = _fixture.CreateAdministrativeContext();
        var page = await new ModerationQueueQueries(context).ListAsync(
            ModerationContentKind.Food,
            ModerationStatusFilter.All,
            $"queue-{marker}",
            new PageRequest(1, 50),
            token);

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, item => Assert.NotEqual(Guid.Empty, item.OwnerTrainerId));
        Assert.Contains(page.Items, item => item.OwnerTrainerId == first.TrainerId);
        Assert.Contains(page.Items, item => item.OwnerTrainerId == second.TrainerId);
        Assert.All(page.Items, item => Assert.Equal("allowed", item.PlatformEnforcementStatus));
    }

    [Fact]
    public async Task ModerationQueue_BlockedFilter_ReturnsOnlyBlockedContent()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);
        var marker = NewDiscriminator();
        var blockedId = await SeedPrivateFoodAsync(seed.TrainerId, $"blocked-{marker}", token);
        await SeedPrivateFoodAsync(seed.TrainerId, $"allowed-{marker}", token);

        await using (var context = _fixture.CreateContext(seed.TrainerId))
        {
            var food = await context.Foods.SingleAsync(item => item.Id == blockedId, token);
            food.Block(PlatformEnforcementReason.FromString("prohibited_content"), Now);
            await context.SaveChangesAsync(token);
        }

        await using var adminContext = _fixture.CreateAdministrativeContext();
        var page = await new ModerationQueueQueries(adminContext).ListAsync(
            ModerationContentKind.Food,
            ModerationStatusFilter.Blocked,
            marker,
            new PageRequest(1, 50),
            token);

        var item = Assert.Single(page.Items);
        Assert.Equal(blockedId, item.Id);
        Assert.Equal("blocked", item.PlatformEnforcementStatus);
        Assert.Equal("prohibited_content", item.PlatformEnforcementReason);
        Assert.NotNull(item.PlatformEnforcedAt);
    }

}
