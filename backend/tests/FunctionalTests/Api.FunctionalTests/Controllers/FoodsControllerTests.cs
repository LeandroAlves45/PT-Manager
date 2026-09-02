using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Contracts.Nutrition;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>Prova o contrato HTTP, o isolamento por tenant e as policies de Foods.</summary>
[Collection(ApiTestCollection.Name)]
public sealed class FoodsControllerTests
{
    private readonly PostgresApiFixture _fixture;

    public FoodsControllerTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_WithTrainerToken_ReturnsCreatedWithLocation()
    {
        var trainer = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainer.TrainerId));

        var response = await client.PostAsJsonAsync(
            "/api/v1/foods",
            new CreateFoodRequest("Arroz basmati", null, 7.5m, 78m, 0.6m, 1.4m),
            Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await ReadJsonAsync(response);
        Assert.Equal("Arroz basmati", body.GetProperty("name").GetString());
        Assert.True(body.GetProperty("kcal").GetDecimal() > 0m);
    }

    [Fact]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        var client = _fixture.Factory.CreateOriginClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/foods",
            new CreateFoodRequest("Sem token", null, 1m, 1m, 1m, null),
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(
            response.Headers.WwwAuthenticate,
            header => header.Scheme == "Bearer");
    }

    [Fact]
    public async Task Create_WithClientToken_ReturnsForbidden()
    {
        var trainerId = Guid.NewGuid();
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), trainerId));

        var response = await client.PostAsJsonAsync(
            "/api/v1/foods",
            new CreateFoodRequest("Cliente não prescreve", null, 1m, 1m, 1m, null),
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithNegativeMacros_ReturnsValidationProblemDetails()
    {
        var trainer = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainer.TrainerId));

        var response = await client.PostAsJsonAsync(
            "/api/v1/foods",
            new CreateFoodRequest("Inválido", null, -1m, 1m, 1m, null),
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.NotEmpty(body.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public async Task Get_WithFoodFromAnotherTenant_ReturnsNotFoundAndNeverForbidden()
    {
        var owner = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);
        var foodId = await NutritionTestData.SeedPrivateFoodAsync(
            _fixture.Factory,
            owner.TrainerId,
            "Alimento do dono",
            Token);

        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(Guid.NewGuid()));

        var response = await client.GetAsync($"/api/v1/foods/{foodId}", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithFoodFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);
        var foodId = await NutritionTestData.SeedPrivateFoodAsync(
            _fixture.Factory,
            owner.TrainerId,
            "Alvo de IDOR",
            Token);

        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(Guid.NewGuid()));

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/foods/{foodId}",
            new UpdateFoodRequest("Renomeado por intruso", null, 1m, 1m, 1m, null),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsOnlyOwnPrivateFoods()
    {
        var owner = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);
        var other = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);

        await NutritionTestData.SeedPrivateFoodAsync(
            _fixture.Factory, owner.TrainerId, "Visível ao dono", Token);
        await NutritionTestData.SeedPrivateFoodAsync(
            _fixture.Factory, other.TrainerId, "Invisível ao dono", Token);

        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(owner.TrainerId));

        var body = await ReadJsonAsync(
            await client.GetAsync("/api/v1/foods?page_number=1&page_size=50", Token));

        var names = body.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .ToArray();

        Assert.Contains("Visível ao dono", names);
        Assert.DoesNotContain("Invisível ao dono", names);
    }

    [Fact]
    public async Task List_StaysWithinQueryBudget()
    {
        var owner = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);
        for (var index = 0; index < 10; index++)
        {
            await NutritionTestData.SeedPrivateFoodAsync(
                _fixture.Factory,
                owner.TrainerId,
                $"Alimento {index}",
                Token);
        }

        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(owner.TrainerId));

        using var scope = CommandCountingInterceptor.BeginScope();

        var response = await client.GetAsync(
            "/api/v1/foods?page_number=1&page_size=50",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            scope.Count <= 2,
            $"Orçamento excedido: {scope.Count} comandos. {string.Join(" | ", scope.Commands)}");
    }

    [Fact]
    public async Task Update_WithOwnFood_ReturnsOk()
    {
        var owner = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);
        var foodId = await NutritionTestData.SeedPrivateFoodAsync(
            _fixture.Factory, owner.TrainerId, "Original", Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(owner.TrainerId));

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/foods/{foodId}",
            new UpdateFoodRequest("Actualizado", null, 8m, 80m, 1m, null),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("Actualizado", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Archive_WithOwnFood_ReturnsNoContent()
    {
        var owner = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);
        var foodId = await NutritionTestData.SeedPrivateFoodAsync(
            _fixture.Factory, owner.TrainerId, "A arquivar", Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(owner.TrainerId));

        var response = await client.PostAsync(
            $"/api/v1/foods/{foodId}/archive",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Reactivate_WithArchivedFood_ReturnsNoContent()
    {
        var owner = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);
        var foodId = await NutritionTestData.SeedInactiveFoodAsync(
            _fixture.Factory, owner.TrainerId, Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(owner.TrainerId));

        var response = await client.PostAsync(
            $"/api/v1/foods/{foodId}/reactivate",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task List_WithSnakeCaseArchivedFilter_ReturnsArchivedFood()
    {
        var owner = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);
        await NutritionTestData.SeedPrivateFoodAsync(
            _fixture.Factory, owner.TrainerId, "Activo", Token);
        await NutritionTestData.SeedInactiveFoodAsync(
            _fixture.Factory, owner.TrainerId, Token);

        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(owner.TrainerId));

        var body = await ReadJsonAsync(
            await client.GetAsync("/api/v1/foods?activity=archived", Token));

        var names = body.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .ToArray();
        Assert.Contains("Alimento inactivo", names);
        Assert.DoesNotContain("Activo", names);
    }

    [Fact]
    public async Task List_WithInvalidActivityFilter_ReturnsBadRequest()
    {
        var owner = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(owner.TrainerId));

        var response = await client.GetAsync("/api/v1/foods?activity=not_a_filter", Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_WithPascalCaseArchivedFilter_BindsCaseInsensitively()
    {
        var owner = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);
        await NutritionTestData.SeedPrivateFoodAsync(
            _fixture.Factory, owner.TrainerId, "Activo", Token);
        await NutritionTestData.SeedInactiveFoodAsync(
            _fixture.Factory, owner.TrainerId, Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(owner.TrainerId));

        var snake = await ReadJsonAsync(
            await client.GetAsync("/api/v1/foods?activity=archived", Token));
        var pascal = await ReadJsonAsync(
            await client.GetAsync("/api/v1/foods?activity=Archived", Token));

        Assert.Equal(
            snake.GetProperty("items").GetArrayLength(),
            pascal.GetProperty("items").GetArrayLength());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 100)]
    public async Task List_WithAcceptedPaginationBoundaries_ReturnsOk(
        int pageNumber,
        int pageSize)
    {
        var owner = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(owner.TrainerId));

        var response = await client.GetAsync(
            $"/api/v1/foods?page_number={pageNumber}&page_size={pageSize}",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task List_WithPageSizeAboveTheLimit_ReturnsBadRequest()
    {
        var owner = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(owner.TrainerId));

        var response = await client.GetAsync(
            "/api/v1/foods?page_number=1&page_size=101",
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _fixture.Factory.CreateOriginClient()
            .GetAsync($"/api/v1/foods/{Guid.NewGuid()}", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithClientToken_ReturnsForbidden()
    {
        var owner = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);
        var foodId = await NutritionTestData.SeedPrivateFoodAsync(
            _fixture.Factory, owner.TrainerId, "Alvo", Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), owner.TrainerId));

        var response = await client.GetAsync($"/api/v1/foods/{foodId}", Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Archive_WithClientToken_ReturnsForbidden()
    {
        var owner = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);
        var foodId = await NutritionTestData.SeedPrivateFoodAsync(
            _fixture.Factory, owner.TrainerId, "Alvo", Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), owner.TrainerId));

        var response = await client.PostAsync(
            $"/api/v1/foods/{foodId}/archive",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_WithClientToken_ReturnsForbidden()
    {
        var owner = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), owner.TrainerId));

        var response = await client.GetAsync("/api/v1/foods", Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync(Token);
        return JsonDocument.Parse(payload).RootElement.Clone();
    }
}
