using Application.Features.Assessments.CheckIns.ListCheckIns;
using Application.Pagination;
using Domain.Entities.Assessments;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Assessments;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Assessments;

[Collection(PostgresCollection.Name)]
public sealed class CheckInReadQueriesTests : ReadQueriesIntegrationTestContext
{
    public CheckInReadQueriesTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CheckInQueries_UnreviewedFilter_ExcludesReviewedAndUnanswered()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);
        await SeedPendingReviewCheckInsAsync(seed, 2, token);

        await using (var context = _fixture.CreateContext(seed.TrainerId))
        {
            // Um respondido passa a revisto e outro fica só agendado.
            var answered = await context.CheckIns
                .OrderBy(item => item.CheckInDate)
                .FirstAsync(token);
            answered.MarkReviewed(Now);
            context.CheckIns.Add(new CheckIn(
                seed.TrainerId, seed.ClientId, LocalToday.AddDays(5), null, Now));
            await context.SaveChangesAsync(token);
        }

        await using var readContext = _fixture.CreateContext(seed.TrainerId);
        var page = await new CheckInQueries(readContext).ListAsync(
            seed.TrainerId,
            null,
            CheckInStatusFilter.Unreviewed,
            null,
            null,
            LocalToday,
            new PageRequest(1, 50),
            token);

        Assert.Equal(1, page.TotalCount);
        Assert.All(page.Items, dto => Assert.Null(dto.ReviewedAt));
    }

    [Fact]
    public async Task CheckInQueries_GetMyNext_ReturnsOwnScheduledCheckInOnly()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);
        var intruder = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);

        await using (var context = _fixture.CreateContext(seed.TrainerId))
        {
            context.CheckIns.Add(new CheckIn(
                seed.TrainerId, seed.ClientId, LocalToday.AddDays(2), null, Now));
            context.CheckIns.Add(new CheckIn(
                seed.TrainerId, seed.ClientId, LocalToday.AddDays(9), null, Now));
            await context.SaveChangesAsync(token);
        }

        await using var readContext = _fixture.CreateContext(seed.TrainerId);
        var queries = new CheckInQueries(readContext);

        var next = await queries.GetMyNextAsync(seed.TrainerId, seed.ClientUserId, LocalToday, token);
        var foreign = await queries.GetMyNextAsync(
            seed.TrainerId, intruder.ClientUserId, LocalToday, token);

        Assert.Equal(LocalToday.AddDays(2), next!.CheckInDate);
        Assert.False(next.IsToday);
        Assert.Null(foreign);
    }

}
