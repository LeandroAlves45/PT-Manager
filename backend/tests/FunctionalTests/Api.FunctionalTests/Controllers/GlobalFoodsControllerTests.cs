using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>Prova que o catálogo global é inacessível fora do contexto administrativo.</summary>
[Collection(ApiTestCollection.Name)]
public sealed class GlobalFoodsControllerTests
{
    private readonly PostgresApiFixture _fixture;

    public GlobalFoodsControllerTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task List_WithTrainerToken_ReturnsForbidden()
    {
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(Guid.NewGuid()));

        var response = await client.GetAsync("/api/v1/global-foods", Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_WithClientToken_ReturnsForbidden()
    {
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), Guid.NewGuid()));

        var response = await client.GetAsync("/api/v1/global-foods", Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_WithSuperuserToken_ReturnsOk()
    {
        var superuserId = await NutritionTestData.SeedSuperuserAsync(_fixture.Factory, Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueSuperuser(superuserId));

        var response = await client.GetAsync("/api/v1/global-foods", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithTrainerToken_ReturnsForbidden()
    {
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(Guid.NewGuid()));

        var response = await client.PostAsJsonAsync(
            "/api/v1/global-foods",
            new { name = "Global", protein = 1m, carbs = 1m, fats = 1m },
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _fixture.Factory.CreateOriginClient()
            .GetAsync("/api/v1/global-foods", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenFoodIsReferenced_ReturnsConflict()
    {
        var seed = await NutritionTestData.SeedReferencedGlobalFoodAsync(
            _fixture.Factory,
            Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueSuperuser(seed.SuperuserId));

        var response = await client.DeleteAsync(
            $"/api/v1/global-foods/{seed.FoodId}",
            Token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("global_food_has_references", body.GetProperty("title").GetString());
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync(Token);
        return JsonDocument.Parse(payload).RootElement.Clone();
    }
}
