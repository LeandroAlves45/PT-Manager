using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Contracts.Portal;
using Api.FunctionalTests.Support;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Suplementos do plano alimentar fora das refeições, escritos pelo personal trainer, e as
/// tomas de hoje dos suplementos do plano escolhido pelo cliente. Contra PostgreSQL real.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class PlanSupplementIntakeTests
{
    private readonly PostgresApiFixture _fixture;

    public PlanSupplementIntakeTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Trainer_WritesSupplementsOutsideTheMeals_AndEditingKeepsTheirIds()
    {
        var tenant = await SeedAsync();
        var trainer = TrainerClient(tenant.TrainerId);

        var created = await ReadJsonAsync(await trainer.PostAsJsonAsync(
            "/api/v1/meal-plans",
            NutritionHttpPayloads.CreateMealPlan(
                tenant.ClientId, tenant.FoodId, tenant.SupplementId, mealCount: 1,
                planSupplements: [PlanSupplement(tenant.CaffeineId, "30 minutos antes do treino", 1)]),
            Token));
        var planId = created.GetProperty("id").GetGuid();
        var outside = Assert.Single(created.GetProperty("plan_supplements").EnumerateArray());
        var outsideId = outside.GetProperty("id").GetGuid();
        var meal = created.GetProperty("meals")[0];

        var edited = await ReadJsonAsync(await trainer.PutAsJsonAsync(
            $"/api/v1/meal-plans/{planId}",
            NutritionHttpPayloads.UpdateMealPlan(
                tenant.FoodId, tenant.SupplementId, [KeepMeal(meal)],
                planSupplements: [PlanSupplement(tenant.CaffeineId, "Antes do treino", 1, outsideId)]),
            Token));
        var emptied = await ReadJsonAsync(await trainer.PutAsJsonAsync(
            $"/api/v1/meal-plans/{planId}",
            NutritionHttpPayloads.UpdateMealPlan(tenant.FoodId, tenant.SupplementId, [KeepMeal(meal)]),
            Token));

        Assert.Equal("30 minutos antes do treino", outside.GetProperty("notes").GetString());
        var kept = Assert.Single(edited.GetProperty("plan_supplements").EnumerateArray());
        Assert.Equal(outsideId, kept.GetProperty("id").GetGuid());
        Assert.Equal("Antes do treino", kept.GetProperty("notes").GetString());
        Assert.Equal(0, emptied.GetProperty("plan_supplements").GetArrayLength());
    }

    [Fact]
    public async Task Trainer_OmittingThePlanSupplements_IsRefused()
    {
        var tenant = await SeedAsync();
        var payload = new
        {
            client_id = tenant.ClientId,
            name = "Sem lista",
            description = (string?)null,
            starts_date = "2026-08-10",
            ends_date = (string?)null,
            calculation = NutritionHttpPayloads.ManualEnergyCalculation(),
            structure = new { meals = Array.Empty<object>() }
        };

        var response = await TrainerClient(tenant.TrainerId).PostAsJsonAsync("/api/v1/meal-plans", payload, Token);

        // O contrato exige a lista: o model binding recusa o pedido antes do validador.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("PlanSupplements", await response.Content.ReadAsStringAsync(Token), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(123456789)]
    [InlineData(10000.01)]
    [InlineData(5.005)]
    public async Task Trainer_QuantityBeyondTheColumn_Is400NotA500(double quantity)
    {
        var tenant = await SeedAsync();

        var response = await TrainerClient(tenant.TrainerId).PostAsJsonAsync(
            "/api/v1/meal-plans",
            NutritionHttpPayloads.CreateMealPlan(
                tenant.ClientId, tenant.FoodId, tenant.SupplementId, mealCount: 1,
                planSupplements: [PlanSupplement(tenant.CaffeineId, null, 1, quantity: (decimal)quantity)]),
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Portal_BeforeTheFirstChoice_HasNoIntakes_EvenWithASinglePlan()
    {
        var tenant = await SeedAsync();
        await CreatePlanAsync(tenant, "Único");
        await PortalTestData.ClearMealPlanSelectionAsync(_fixture.Factory, tenant.TrainerId, tenant.ClientUserId, Token);

        var response = await Portal(tenant).GetAsync("/api/v1/portal/my-plan-supplements/today", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("portal_nutrition_plan_not_selected", await TitleAsync(response));
    }

    [Fact]
    public async Task Portal_MarksAndUnmarksTodaysIntake_OfMealAndOutsideMealSupplements()
    {
        var tenant = await SeedAsync();
        var planId = await CreatePlanAsync(tenant, "Treino");
        var portal = Portal(tenant);
        await Select(portal, planId);

        var today = await ReadJsonAsync(await portal.GetAsync("/api/v1/portal/my-plan-supplements/today", Token));
        var items = today.GetProperty("items").EnumerateArray().ToList();
        var inMeal = Assert.Single(items, item => item.GetProperty("placement").GetString() == "meal");
        var outside = Assert.Single(items, item => item.GetProperty("placement").GetString() == "plan");
        var outsideId = outside.GetProperty("id").GetGuid();

        var mark = await portal.PutAsync($"/api/v1/portal/my-plan-supplements/{outsideId}/intakes/today", null, Token);
        var again = await portal.PutAsync($"/api/v1/portal/my-plan-supplements/{outsideId}/intakes/today", null, Token);
        var afterMark = await ItemAsync(portal, outsideId);
        var home = await ReadJsonAsync(await portal.GetAsync("/api/v1/portal/home", Token));
        var unmark = await portal.DeleteAsync($"/api/v1/portal/my-plan-supplements/{outsideId}/intakes/today", Token);
        var afterUnmark = await ItemAsync(portal, outsideId);

        Assert.Equal(planId, today.GetProperty("meal_plan_id").GetGuid());
        Assert.Equal("Refeição 1", inMeal.GetProperty("meal_type").GetString());
        Assert.Equal(JsonValueKind.Null, outside.GetProperty("taken_at").ValueKind);
        Assert.Equal(HttpStatusCode.OK, mark.StatusCode);
        // Repetir devolve a mesma toma (o PostgreSQL guarda microssegundos).
        Assert.Equal(
            (await ReadJsonAsync(mark)).GetProperty("taken_at").GetDateTime().Ticks / 10,
            (await ReadJsonAsync(again)).GetProperty("taken_at").GetDateTime().Ticks / 10);
        Assert.NotEqual(JsonValueKind.Null, afterMark.GetProperty("taken_at").ValueKind);
        Assert.True(home.GetProperty("supplements").GetProperty("taken_count").GetInt32() >= 1);
        Assert.Equal(HttpStatusCode.NoContent, unmark.StatusCode);
        Assert.Equal(JsonValueKind.Null, afterUnmark.GetProperty("taken_at").ValueKind);
        Assert.Equal(0, await CountIntakesAsync(tenant));
    }

    [Fact]
    public async Task Portal_SwitchingPlans_ShowsThatPlansIntakes_WithoutMixing()
    {
        var tenant = await SeedAsync();
        var first = await CreatePlanAsync(tenant, "Dias de treino");
        var second = await CreatePlanAsync(tenant, "Dias de descanso");
        var portal = Portal(tenant);
        await Select(portal, first);
        var firstItem = (await ReadJsonAsync(await portal.GetAsync("/api/v1/portal/my-plan-supplements/today", Token)))
            .GetProperty("items")[0].GetProperty("id").GetGuid();
        await portal.PutAsync($"/api/v1/portal/my-plan-supplements/{firstItem}/intakes/today", null, Token);

        await Select(portal, second);
        var secondToday = await ReadJsonAsync(await portal.GetAsync("/api/v1/portal/my-plan-supplements/today", Token));
        var markOther = await portal.PutAsync($"/api/v1/portal/my-plan-supplements/{firstItem}/intakes/today", null, Token);
        await Select(portal, first);
        var backToFirst = await ItemAsync(portal, firstItem);

        Assert.All(secondToday.GetProperty("items").EnumerateArray(),
            item => Assert.Equal(JsonValueKind.Null, item.GetProperty("taken_at").ValueKind));
        Assert.DoesNotContain(secondToday.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == firstItem);
        // Uma prescrição de outro plano não se marca enquanto não for o plano aberto.
        Assert.Equal(HttpStatusCode.NotFound, markOther.StatusCode);
        Assert.Equal("portal_plan_supplement_not_found", await TitleAsync(markOther));
        Assert.NotEqual(JsonValueKind.Null, backToFirst.GetProperty("taken_at").ValueKind);
    }

    [Fact]
    public async Task Portal_ArchivingTheChosenPlan_LeavesNoIntakesUntilTheClientChoosesAgain()
    {
        var tenant = await SeedAsync();
        var planId = await CreatePlanAsync(tenant, "Arquivável");
        await CreatePlanAsync(tenant, "Outro");
        var portal = Portal(tenant);
        await Select(portal, planId);

        await TrainerClient(tenant.TrainerId).PostAsync($"/api/v1/meal-plans/{planId}/archive", null, Token);
        var response = await portal.GetAsync("/api/v1/portal/my-plan-supplements/today", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("portal_nutrition_plan_not_selected", await TitleAsync(response));
    }

    [Fact]
    public async Task Portal_ArchivedCatalogSupplement_IsUnavailable_CannotBeMarked_ButCanBeUnmarked()
    {
        var tenant = await SeedAsync();
        var planId = await CreatePlanAsync(tenant, "Com cafeína");
        var portal = Portal(tenant);
        await Select(portal, planId);
        var outside = await OutsideItemAsync(portal);
        await portal.PutAsync($"/api/v1/portal/my-plan-supplements/{outside}/intakes/today", null, Token);

        await ExecuteAsync(db => db.Supplements.IgnoreQueryFilters()
            .Where(item => item.Id == tenant.CaffeineId)
            .ExecuteUpdateAsync(set => set.SetProperty(item => item.IsActive, false), Token));
        var listed = await ItemAsync(portal, outside);
        var unmark = await portal.DeleteAsync($"/api/v1/portal/my-plan-supplements/{outside}/intakes/today", Token);
        var mark = await portal.PutAsync($"/api/v1/portal/my-plan-supplements/{outside}/intakes/today", null, Token);

        Assert.True(listed.GetProperty("is_unavailable").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, unmark.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, mark.StatusCode);
        Assert.Equal("portal_plan_supplement_unavailable", await TitleAsync(mark));
    }

    [Fact]
    public async Task Portal_ThePrescriptionLeavingThePlan_KeepsTheIntakeAsHistory()
    {
        var tenant = await SeedAsync();
        var planId = await CreatePlanAsync(tenant, "Histórico");
        var portal = Portal(tenant);
        await Select(portal, planId);
        var outside = await OutsideItemAsync(portal);
        await portal.PutAsync($"/api/v1/portal/my-plan-supplements/{outside}/intakes/today", null, Token);
        var trainer = TrainerClient(tenant.TrainerId);
        var meal = (await ReadJsonAsync(await trainer.GetAsync($"/api/v1/meal-plans/{planId}", Token)))
            .GetProperty("meals")[0];

        await trainer.PutAsJsonAsync(
            $"/api/v1/meal-plans/{planId}",
            NutritionHttpPayloads.UpdateMealPlan(tenant.FoodId, tenant.SupplementId, [KeepMeal(meal)]),
            Token);

        var intake = await QueryAsync(db => db.ClientMealPlanSupplementIntakes.IgnoreQueryFilters()
            .Where(item => item.MealPlanId == planId)
            .Select(item => new { item.MealPlanSupplementId, item.SupplementId })
            .SingleAsync(Token));
        Assert.Null(intake.MealPlanSupplementId);
        Assert.Equal(tenant.CaffeineId, intake.SupplementId);
    }

    [Fact]
    public async Task Portal_AnotherClientsPrescription_IsNotFound()
    {
        var owner = await SeedAsync();
        var planId = await CreatePlanAsync(owner, "Do dono");
        await Select(Portal(owner), planId);
        var outside = await OutsideItemAsync(Portal(owner));
        var intruder = await SeedAsync();
        await Select(Portal(intruder), await CreatePlanAsync(intruder, "Do intruso"));

        var response = await Portal(intruder).PutAsync(
            $"/api/v1/portal/my-plan-supplements/{outside}/intakes/today", null, Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<PlanTenant> SeedAsync()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var clientId = await PortalTestData.GetClientIdAsync(_fixture.Factory, trainerId, clientUserId, Token);
        var ids = await QueryAsync(async db => new
        {
            Food = await db.Foods.IgnoreQueryFilters()
                .Where(item => item.OwnerTrainerId == trainerId).Select(item => item.Id).FirstAsync(Token),
            Supplement = await db.Supplements.IgnoreQueryFilters()
                .Where(item => item.OwnerTrainerId == trainerId).Select(item => item.Id).FirstAsync(Token)
        });
        var caffeine = new Domain.Entities.Supplements.Supplement(
            trainerId, trainerId, "Cafeína", null, "mg", "200 mg", "Antes do treino", null,
            TrainerTenantSeeder.SeedInstant);
        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<Application.Common.Abstractions.ITenantContextInitializer>()
                .Establish(trainerId, trainerId, "trainer", Application.Common.Abstractions.TenantOrigin.System, false);
            var db = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
            db.Supplements.Add(caffeine);
            await db.SaveChangesAsync(Token);
        }

        return new PlanTenant(trainerId, clientUserId, clientId, ids.Food, ids.Supplement, caffeine.Id);
    }

    // O nome só documenta o cenário; o payload dos testes de nutrição usa um nome fixo.
    private async Task<Guid> CreatePlanAsync(PlanTenant tenant, string name)
    {
        var response = await TrainerClient(tenant.TrainerId).PostAsJsonAsync(
            "/api/v1/meal-plans",
            NutritionHttpPayloads.CreateMealPlan(
                tenant.ClientId, tenant.FoodId, tenant.SupplementId, mealCount: 1,
                planSupplements: [PlanSupplement(tenant.CaffeineId, "30 minutos antes do treino", 1)]),
            Token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task Select(HttpClient portal, Guid planId) =>
        Assert.Equal(HttpStatusCode.NoContent, (await ApiJsonPayload.PutAsync(
            portal, "/api/v1/portal/my-meal-plans/selection", new SelectMyMealPlanRequest(planId), Token)).StatusCode);

    private static async Task<Guid> OutsideItemAsync(HttpClient portal) =>
        (await ReadJsonAsync(await portal.GetAsync("/api/v1/portal/my-plan-supplements/today", Token)))
            .GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("placement").GetString() == "plan")
            .GetProperty("id").GetGuid();

    private static async Task<JsonElement> ItemAsync(HttpClient portal, Guid itemId) =>
        (await ReadJsonAsync(await portal.GetAsync("/api/v1/portal/my-plan-supplements/today", Token)))
            .GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == itemId);

    private async Task<int> CountIntakesAsync(PlanTenant tenant) =>
        await QueryAsync(db => db.ClientMealPlanSupplementIntakes.IgnoreQueryFilters()
            .CountAsync(item => item.ClientId == tenant.ClientId, Token));

    private async Task<T> QueryAsync<T>(Func<PtManagerDbContext, Task<T>> query)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<PtManagerDbContext>());
    }

    private async Task ExecuteAsync(Func<PtManagerDbContext, Task<int>> update)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        await update(scope.ServiceProvider.GetRequiredService<PtManagerDbContext>());
    }

    private static object PlanSupplement(Guid supplementId, string? notes, int order, Guid? id = null, decimal quantity = 200m) => new
    {
        id,
        supplement_id = supplementId,
        notes,
        quantity,
        order_number = order
    };

    private static object KeepMeal(JsonElement meal) => new
    {
        id = meal.GetProperty("id").GetGuid(),
        meal_type = meal.GetProperty("meal_type").GetString(),
        order_number = meal.GetProperty("order_number").GetInt32(),
        items = meal.GetProperty("items").EnumerateArray().Select(item => new
        {
            id = item.GetProperty("id").GetGuid(),
            food_id = item.GetProperty("food_id").GetGuid(),
            quantity_in_grams = item.GetProperty("quantity_in_grams").GetDecimal(),
            order_number = item.GetProperty("order_number").GetInt32()
        }).ToArray(),
        supplements = meal.GetProperty("supplements").EnumerateArray().Select(item => new
        {
            id = item.GetProperty("id").GetGuid(),
            supplement_id = item.GetProperty("supplement_id").GetGuid(),
            notes = item.GetProperty("notes").GetString(),
            quantity = item.GetProperty("quantity").GetDecimal(),
            order_number = item.GetProperty("order_number").GetInt32()
        }).ToArray()
    };

    private HttpClient TrainerClient(Guid trainerId) =>
        _fixture.Factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueTrainer(trainerId));

    private HttpClient Portal(PlanTenant tenant) =>
        _fixture.Factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueClient(tenant.ClientUserId, tenant.TrainerId));

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await ReadJsonAsync(response)).GetProperty("title").GetString();

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ApiJsonPayload.Options, Token);

    private sealed record PlanTenant(
        Guid TrainerId,
        Guid ClientUserId,
        Guid ClientId,
        Guid FoodId,
        Guid SupplementId,
        Guid CaffeineId);
}
