using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Contracts.Supplements;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>Prova a curadoria do catálogo global de suplementos.</summary>
[Collection(ApiTestCollection.Name)]
public sealed class GlobalSupplementsControllerTests
{
    private readonly PostgresApiFixture _fixture;

    public GlobalSupplementsControllerTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_WithSuperuserToken_ReturnsCreated()
    {
        var client = await SuperuserClientAsync();

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/global-supplements",
            NewGlobalSupplement($"Creatina global {Guid.NewGuid():N}"),
            Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        var response = await ApiJsonPayload.PostAsync(
            _fixture.Factory.CreateOriginClient(),
            "/api/v1/global-supplements",
            NewGlobalSupplement("Sem token"),
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithTrainerToken_ReturnsForbidden()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(tenant.TrainerId));

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/global-supplements",
            NewGlobalSupplement("Personal trainer não cura"),
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithClientToken_ReturnsForbidden()
    {
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), Guid.NewGuid()));

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/global-supplements",
            NewGlobalSupplement("Cliente não cura"),
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_WithSuperuserToken_ReturnsOk()
    {
        var client = await SuperuserClientAsync();

        var response = await client.GetAsync("/api/v1/global-supplements", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithUnknownIdentifier_ReturnsNotFound()
    {
        var client = await SuperuserClientAsync();

        var response = await client.GetAsync(
            $"/api/v1/global-supplements/{Guid.NewGuid()}",
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ArchiveThenReactivate_TogglesCatalogAvailability()
    {
        var client = await SuperuserClientAsync();
        var created = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/global-supplements",
            NewGlobalSupplement($"Global toggle {Guid.NewGuid():N}"),
            Token);
        var supplementId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        var archive = await client.PostAsync(
            $"/api/v1/global-supplements/{supplementId}/archive",
            content: null,
            Token);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var archived = await ReadJsonAsync(
            await client.GetAsync($"/api/v1/global-supplements/{supplementId}", Token));
        Assert.False(archived.GetProperty("is_active").GetBoolean());

        var reactivate = await client.PostAsync(
            $"/api/v1/global-supplements/{supplementId}/reactivate",
            content: null,
            Token);
        Assert.Equal(HttpStatusCode.NoContent, reactivate.StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutReferences_RemovesTheSupplement()
    {
        var client = await SuperuserClientAsync();
        var created = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/global-supplements",
            NewGlobalSupplement($"Descartável {Guid.NewGuid():N}"),
            Token);
        var supplementId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        var deleted = await client.DeleteAsync(
            $"/api/v1/global-supplements/{supplementId}",
            Token);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var afterDelete = await client.GetAsync(
            $"/api/v1/global-supplements/{supplementId}",
            Token);
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    private static CreateGlobalSupplementRequest NewGlobalSupplement(string name) => new(
        name,
        null,
        "grams",
        "5 g",
        "Daily",
        null);

    private async Task<HttpClient> SuperuserClientAsync()
    {
        var superuserId = await TrainingTestData.SeedSuperuserAsync(_fixture.Factory, Token);
        return _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueSuperuser(superuserId));
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ApiJsonPayload.Options, Token);
}
