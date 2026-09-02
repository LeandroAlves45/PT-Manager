using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>Prova contratos HTTP, reconciliação substitutiva e isolamento de MealPlans.</summary>
[Collection(ApiTestCollection.Name)]
public sealed class MealPlansControllerTests
{
    private readonly PostgresApiFixture _fixture;

    public MealPlansControllerTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_WithTrainerToken_ReturnsCreatedWithMealsAndTotals()
    {
        var catalog = await NutritionTestData.SeedMealPlanCatalogAsync(_fixture.Factory, Token);
        var client = TrainerClient(catalog.TrainerId);

        var response = await client.PostAsJsonAsync(
            "/api/v1/meal-plans",
            NutritionHttpPayloads.CreateMealPlan(
                catalog.ClientId,
                catalog.FoodId,
                catalog.SupplementId,
                mealCount: 2),
            Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await ReadJsonAsync(response);
        Assert.Equal(2, body.GetProperty("meals").GetArrayLength());
        Assert.True(body.GetProperty("actual_totals").GetProperty("kcal").GetDecimal() > 0m);
    }

    [Fact]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        var catalog = await NutritionTestData.SeedMealPlanCatalogAsync(_fixture.Factory, Token);

        var response = await _fixture.Factory.CreateOriginClient().PostAsJsonAsync(
            "/api/v1/meal-plans",
            NutritionHttpPayloads.CreateMealPlan(
                catalog.ClientId,
                catalog.FoodId,
                catalog.SupplementId),
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithClientToken_ReturnsForbidden()
    {
        var catalog = await NutritionTestData.SeedMealPlanCatalogAsync(_fixture.Factory, Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), catalog.TrainerId));

        var response = await client.PostAsJsonAsync(
            "/api/v1/meal-plans",
            NutritionHttpPayloads.CreateMealPlan(
                catalog.ClientId,
                catalog.FoodId,
                catalog.SupplementId),
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithInactiveFood_ReturnsConflict()
    {
        var catalog = await NutritionTestData.SeedMealPlanCatalogAsync(_fixture.Factory, Token);
        var inactiveFoodId = await NutritionTestData.SeedInactiveFoodAsync(
            _fixture.Factory,
            catalog.TrainerId,
            Token);
        var client = TrainerClient(catalog.TrainerId);

        var response = await client.PostAsJsonAsync(
            "/api/v1/meal-plans",
            NutritionHttpPayloads.CreateMealPlan(
                catalog.ClientId,
                inactiveFoodId,
                catalog.SupplementId),
            Token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("nutrition_catalog_reference_inactive", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Create_WithMissingFood_ReturnsNotFound()
    {
        var catalog = await NutritionTestData.SeedMealPlanCatalogAsync(_fixture.Factory, Token);
        var client = TrainerClient(catalog.TrainerId);

        var response = await client.PostAsJsonAsync(
            "/api/v1/meal-plans",
            NutritionHttpPayloads.CreateMealPlan(
                catalog.ClientId,
                Guid.NewGuid(),
                catalog.SupplementId),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("nutrition_catalog_reference_not_found", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Get_WithMealPlanFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await NutritionTestData.SeedMealPlanAsync(_fixture.Factory, Token);
        var intruder = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);

        var response = await TrainerClient(intruder.TrainerId)
            .GetAsync($"/api/v1/meal-plans/{owner.MealPlanId}", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithMealPlanFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await NutritionTestData.SeedMealPlanAsync(_fixture.Factory, Token);
        var intruder = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);

        var response = await TrainerClient(intruder.TrainerId).PutAsJsonAsync(
            $"/api/v1/meal-plans/{owner.MealPlanId}",
            NutritionHttpPayloads.UpdateMealPlan(
                owner.FoodId,
                owner.SupplementId,
                [SingleMealPayload(owner.FoodId, owner.SupplementId, 1)]),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_OmittingMeal_RemovesItFromThePlan()
    {
        var seed = await NutritionTestData.SeedMealPlanAsync(
            _fixture.Factory,
            Token,
            mealCount: 2);
        var client = TrainerClient(seed.TrainerId);

        var detail = await ReadJsonAsync(
            await client.GetAsync($"/api/v1/meal-plans/{seed.MealPlanId}", Token));
        var keptMeal = detail.GetProperty("meals").EnumerateArray()
            .OrderBy(meal => meal.GetProperty("order_number").GetInt32())
            .First();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/meal-plans/{seed.MealPlanId}",
            NutritionHttpPayloads.UpdateMealPlan(
                seed.FoodId,
                seed.SupplementId,
                [ExistingMealPayload(keptMeal, seed.FoodId, seed.SupplementId)]),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await ReadJsonAsync(
            await client.GetAsync($"/api/v1/meal-plans/{seed.MealPlanId}", Token));
        Assert.Equal(1, updated.GetProperty("meals").GetArrayLength());
    }

    [Fact]
    public async Task Archive_WithOwnMealPlan_ReturnsNoContent()
    {
        var seed = await NutritionTestData.SeedMealPlanAsync(_fixture.Factory, Token);
        var client = TrainerClient(seed.TrainerId);

        var response = await client.PostAsync(
            $"/api/v1/meal-plans/{seed.MealPlanId}/archive",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Reactivate_WithArchivedMealPlan_ReturnsNoContent()
    {
        var seed = await NutritionTestData.SeedMealPlanAsync(_fixture.Factory, Token);
        var client = TrainerClient(seed.TrainerId);

        await client.PostAsync(
            $"/api/v1/meal-plans/{seed.MealPlanId}/archive",
            content: null,
            Token);

        var response = await client.PostAsync(
            $"/api/v1/meal-plans/{seed.MealPlanId}/reactivate",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task List_StaysWithinQueryBudget()
    {
        var seed = await NutritionTestData.SeedMealPlanAsync(_fixture.Factory, Token);
        for (var index = 0; index < 9; index++)
            await NutritionTestData.SeedMealPlanAsync(_fixture.Factory, Token);

        var client = TrainerClient(seed.TrainerId);
        using var scope = CommandCountingInterceptor.BeginScope();

        var response = await client.GetAsync(
            "/api/v1/meal-plans?page_number=1&page_size=50",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            scope.Count <= 2,
            $"Orçamento excedido: {scope.Count} comandos. {string.Join(" | ", scope.Commands)}");
    }

    [Fact]
    public async Task Get_WithTenMeals_StaysWithinQueryBudget()
    {
        var seed = await NutritionTestData.SeedMealPlanAsync(
            _fixture.Factory,
            Token,
            mealCount: 10);
        var client = TrainerClient(seed.TrainerId);

        using var scope = CommandCountingInterceptor.BeginScope();

        var response = await client.GetAsync(
            $"/api/v1/meal-plans/{seed.MealPlanId}",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Medido em execução: plano + refeições + itens + suplementos = 4 comandos fixos.
        Assert.True(
            scope.Count <= 4,
            $"Orçamento excedido: {scope.Count} comandos. {string.Join(" | ", scope.Commands)}");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 100)]
    public async Task List_WithAcceptedPaginationBoundaries_ReturnsOk(
        int pageNumber,
        int pageSize)
    {
        var trainer = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);

        var response = await TrainerClient(trainer.TrainerId).GetAsync(
            $"/api/v1/meal-plans?page_number={pageNumber}&page_size={pageSize}",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task List_WithPageSizeAboveTheLimit_ReturnsBadRequest()
    {
        var trainer = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);

        var response = await TrainerClient(trainer.TrainerId).GetAsync(
            "/api/v1/meal-plans?page_number=1&page_size=101",
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.NotEmpty(body.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public async Task List_WithClientToken_ReturnsForbidden()
    {
        var trainer = await NutritionTestData.SeedTrainerAsync(_fixture.Factory, Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), trainer.TrainerId));

        var response = await client.GetAsync("/api/v1/meal-plans", Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithClientToken_ReturnsForbidden()
    {
        var seed = await NutritionTestData.SeedMealPlanAsync(_fixture.Factory, Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), seed.TrainerId));

        var response = await client.GetAsync($"/api/v1/meal-plans/{seed.MealPlanId}", Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithClientToken_ReturnsForbidden()
    {
        var seed = await NutritionTestData.SeedMealPlanAsync(_fixture.Factory, Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), seed.TrainerId));

        var response = await client.PutAsJsonAsync(
            $"/api/v1/meal-plans/{seed.MealPlanId}",
            NutritionHttpPayloads.UpdateMealPlan(
                seed.FoodId,
                seed.SupplementId,
                [SingleMealPayload(seed.FoodId, seed.SupplementId, 1)]),
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Archive_WithClientToken_ReturnsForbidden()
    {
        var seed = await NutritionTestData.SeedMealPlanAsync(_fixture.Factory, Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), seed.TrainerId));

        var response = await client.PostAsync(
            $"/api/v1/meal-plans/{seed.MealPlanId}/archive",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static object SingleMealPayload(Guid foodId, Guid supplementId, int order) => new
    {
        id = (Guid?)null,
        meal_type = "Única",
        order_number = order,
        items = new[]
        {
            new
            {
                id = (Guid?)null,
                food_id = foodId,
                quantity_in_grams = 120m,
                order_number = 1
            }
        },
        supplements = new[]
        {
            new
            {
                id = (Guid?)null,
                supplement_id = supplementId,
                notes = (string?)null,
                quantity = 5m,
                order_number = 1
            }
        }
    };

    private static object ExistingMealPayload(
        JsonElement meal,
        Guid foodId,
        Guid supplementId) => new
        {
            id = meal.GetProperty("id").GetGuid(),
            meal_type = meal.GetProperty("meal_type").GetString(),
            order_number = meal.GetProperty("order_number").GetInt32(),
            items = meal.GetProperty("items").EnumerateArray()
            .Select(item => new
            {
                id = item.GetProperty("id").GetGuid(),
                food_id = item.GetProperty("food_id").GetGuid(),
                quantity_in_grams = item.GetProperty("quantity_in_grams").GetDecimal(),
                order_number = item.GetProperty("order_number").GetInt32()
            })
            .ToArray(),
            supplements = meal.GetProperty("supplements").EnumerateArray()
            .Select(item => new
            {
                id = item.GetProperty("id").GetGuid(),
                supplement_id = item.GetProperty("supplement_id").GetGuid(),
                notes = item.TryGetProperty("notes", out var notes) && notes.ValueKind != JsonValueKind.Null
                    ? notes.GetString()
                    : null,
                quantity = item.GetProperty("quantity").GetDecimal(),
                order_number = item.GetProperty("order_number").GetInt32()
            })
            .ToArray()
        };

    private HttpClient TrainerClient(Guid trainerId) =>
        _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainerId));

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync(Token);
        return JsonDocument.Parse(payload).RootElement.Clone();
    }
}
