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

    // [6E2] NOVO: enums da query aceitam o nome snake_case publicado no OpenAPI e o nome do
    // membro, sem distinguir maiúsculas; números e nomes desconhecidos dão 400.
    [Theory]
    [InlineData("cancelled_by_client", HttpStatusCode.OK)]
    [InlineData("CancelledByClient", HttpStatusCode.OK)]
    [InlineData("CANCELLED_BY_CLIENT", HttpStatusCode.OK)]
    [InlineData("no_show", HttpStatusCode.OK)]
    [InlineData("NoShow", HttpStatusCode.OK)]
    [InlineData("scheduled", HttpStatusCode.OK)]
    [InlineData("", HttpStatusCode.OK)]
    [InlineData("1", HttpStatusCode.BadRequest)]
    [InlineData("99", HttpStatusCode.BadRequest)]
    [InlineData("cancelled-by-client", HttpStatusCode.BadRequest)]
    public async Task SessionsListing_BindsStatusByContractOrMemberName(
        string status,
        HttpStatusCode expected)
    {
        var token = TestContext.Current.CancellationToken;
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(
            _fixture.Factory, $"sst-{Guid.NewGuid():N}", token);

        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainer.TrainerId));

        var response = await client.GetAsync($"/api/v1/sessions?status={status}", token);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("allowed", HttpStatusCode.OK)]
    [InlineData("Blocked", HttpStatusCode.OK)]
    [InlineData("ALL", HttpStatusCode.OK)]
    [InlineData("pending_review", HttpStatusCode.BadRequest)]
    [InlineData("2", HttpStatusCode.BadRequest)]
    public async Task ModerationQueue_BindsStatusByContractOrMemberName(
        string status,
        HttpStatusCode expected)
    {
        var token = TestContext.Current.CancellationToken;
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueSuperuser(Guid.NewGuid()));

        var response = await client.GetAsync(
            $"/api/v1/admin/content-moderation/foods?status={status}&page_size=5", token);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task EnumQueryFilter_RejectsAnEmptyRequiredValue()
    {
        var token = TestContext.Current.CancellationToken;
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(
            _fixture.Factory, $"act-{Guid.NewGuid():N}", token);

        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainer.TrainerId));

        var empty = await client.GetAsync("/api/v1/clients?activity=", token);
        var archived = await client.GetAsync("/api/v1/clients?activity=archived", token);

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
    }
}
