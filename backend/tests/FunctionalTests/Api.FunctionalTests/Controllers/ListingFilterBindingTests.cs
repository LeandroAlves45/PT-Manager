using System.Net;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

[Collection(ApiTestCollection.Name)]
public sealed class ListingFilterBindingTests : ApiReadEndpointsTestContext
{
    public ListingFilterBindingTests(PostgresApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CheckInsListing_AcceptsUnreviewedAndRejectsUnknownStatus()
    {
        var token = TestContext.Current.CancellationToken;
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(
            _fixture.Factory, $"chk-{Guid.NewGuid():N}", token);

        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainer.TrainerId));

        var accepted = await client.GetAsync("/api/v1/check-ins?status=unreviewed", token);
        var rejected = await client.GetAsync("/api/v1/check-ins?status=pending_review", token);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }

    [Fact]
    public async Task ListingFilters_BindQueryStringAndValidateRanges()
    {
        var token = TestContext.Current.CancellationToken;
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(
            _fixture.Factory, $"flt-{Guid.NewGuid():N}", token);

        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainer.TrainerId));

        var clients = await client.GetAsync(
            "/api/v1/clients?without_training_plan=true", token);
        var plans = await client.GetAsync(
            "/api/v1/training-plans?ends_from=2026-09-01&ends_to=2026-09-30", token);
        var mealPlans = await client.GetAsync(
            "/api/v1/meal-plans?ends_from=2026-09-01&ends_to=2026-09-30", token);
        var invalid = await client.GetAsync(
            "/api/v1/training-plans?ends_from=2026-09-30&ends_to=2026-09-01", token);

        Assert.Equal(HttpStatusCode.OK, clients.StatusCode);
        Assert.Equal(HttpStatusCode.OK, plans.StatusCode);
        Assert.Equal(HttpStatusCode.OK, mealPlans.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var problem = await invalid.Content.ReadAsStringAsync(token);
        Assert.Contains("training_plan_ends_range_invalid", problem, StringComparison.Ordinal);
    }

}
