using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Contracts.Supplements;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>Prova o catálogo privado de suplementos do personal trainer.</summary>
[Collection(ApiTestCollection.Name)]
public sealed class SupplementsControllerTests
{
    private readonly PostgresApiFixture _fixture;

    public SupplementsControllerTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_WithTrainerToken_ReturnsCreated()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/supplements",
            NewSupplement("Creatina"),
            Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        var response = await ApiJsonPayload.PostAsync(
            _fixture.Factory.CreateOriginClient(),
            "/api/v1/supplements",
            NewSupplement("Sem token"),
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithClientToken_ReturnsForbidden()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), tenant.TrainerId));

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/supplements",
            NewSupplement("Cliente não prescreve"),
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithEmptyName_ReturnsBadRequest()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);

        var response = await ApiJsonPayload.PostAsync(
            TrainerClient(tenant.TrainerId),
            "/api/v1/supplements",
            NewSupplement("   "),
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_WithTrainerToken_ReturnsPagedResults()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/supplements",
            NewSupplement("ZMA"),
            Token);

        var response = await client.GetAsync("/api/v1/supplements", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithSupplementFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var created = await ApiJsonPayload.PostAsync(
            TrainerClient(owner.TrainerId),
            "/api/v1/supplements",
            NewSupplement("Privado"),
            Token);
        var supplementId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var response = await TrainerClient(intruder.TrainerId)
            .GetAsync($"/api/v1/supplements/{supplementId}", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithTrainerToken_ReturnsOk()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);
        var created = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/supplements",
            NewSupplement("Beta-alanina"),
            Token);
        var supplementId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        var response = await ApiJsonPayload.PatchAsync(
            client,
            $"/api/v1/supplements/{supplementId}",
            NewSupplement("Beta-alanina 3.2g"),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ArchiveThenReactivate_TogglesAvailability()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);
        var created = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/supplements",
            NewSupplement("Glutamina"),
            Token);
        var supplementId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        var archive = await client.PostAsync(
            $"/api/v1/supplements/{supplementId}/archive",
            content: null,
            Token);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var archived = await ReadJsonAsync(
            await client.GetAsync($"/api/v1/supplements/{supplementId}", Token));
        Assert.False(archived.GetProperty("is_active").GetBoolean());

        var reactivate = await client.PostAsync(
            $"/api/v1/supplements/{supplementId}/reactivate",
            content: null,
            Token);
        Assert.Equal(HttpStatusCode.NoContent, reactivate.StatusCode);
    }

    private static CreateSupplementRequest NewSupplement(string name) => new(
        name,
        null,
        "grams",
        "5 g",
        "Daily",
        null);

    private HttpClient TrainerClient(Guid trainerId) =>
        _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainerId));

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ApiJsonPayload.Options, Token);
}
