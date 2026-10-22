using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Contracts.Portal;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Escolha do plano alimentar e filtro de dia do treino no portal, contra PostgreSQL real.
/// O cliente semeado já escolheu o plano «Portal meal plan»; o plano de treino começa a
/// 2 de setembro de 2026 e tem treino às terças.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class ClientPortalMealPlansAndDaysTests
{
    private static readonly TimeZoneInfo Lisbon = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    private readonly PostgresApiFixture _fixture;

    public ClientPortalMealPlansAndDaysTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static DateOnly TodayInLisbon =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Lisbon));

    [Fact]
    public async Task ListMyMealPlans_ReturnsOnlyOwnActivePlansWithSelection()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var extraId = await PortalTestData.SeedExtraMealPlanAsync(
            _fixture.Factory, trainerId, clientUserId, "Dias de descanso", Token);
        // Outro cliente do mesmo personal trainer, com o seu plano.
        var (_, otherUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token, trainerId);
        var client = PortalClient(trainerId, clientUserId);

        var response = await client.GetAsync("/api/v1/portal/my-meal-plans", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        var selected = Assert.Single(items, item => item.GetProperty("is_selected").GetBoolean());
        Assert.Equal("Portal meal plan", selected.GetProperty("name").GetString());
        Assert.Equal(1, selected.GetProperty("meal_count").GetInt32());
        var extra = Assert.Single(items, item => item.GetProperty("id").GetGuid() == extraId);
        Assert.False(extra.GetProperty("is_selected").GetBoolean());
        Assert.DoesNotContain(items, item => item.TryGetProperty("calculation_snapshot", out _));
        Assert.NotEqual(clientUserId, otherUserId);
    }

    [Fact]
    public async Task SelectMyMealPlan_SwitchesPlanAndKeepsItOnTheAccount()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var extraId = await PortalTestData.SeedExtraMealPlanAsync(
            _fixture.Factory, trainerId, clientUserId, "Dias de descanso", Token);
        var client = PortalClient(trainerId, clientUserId);

        var select = await ApiJsonPayload.PutAsync(
            client, "/api/v1/portal/my-meal-plans/selection", new SelectMyMealPlanRequest(extraId), Token);

        Assert.Equal(HttpStatusCode.NoContent, select.StatusCode);
        // Outro aparelho (outro cliente HTTP) abre o mesmo plano.
        var nutrition = await PortalClient(trainerId, clientUserId)
            .GetAsync("/api/v1/portal/my-nutrition", Token);
        Assert.Equal(HttpStatusCode.OK, nutrition.StatusCode);
        Assert.Equal("Dias de descanso", (await ReadJsonAsync(nutrition)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task SelectMyMealPlan_PlanOfAnotherClient_ReturnsNotFoundAndKeepsSelection()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var (_, otherUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token, trainerId);
        var foreignPlanId = await PortalTestData.SeedExtraMealPlanAsync(
            _fixture.Factory, trainerId, otherUserId, "Plano do outro", Token);
        var before = await PortalTestData.GetSelectedMealPlanIdAsync(
            _fixture.Factory, trainerId, clientUserId, Token);

        var response = await ApiJsonPayload.PutAsync(
            PortalClient(trainerId, clientUserId),
            "/api/v1/portal/my-meal-plans/selection",
            new SelectMyMealPlanRequest(foreignPlanId),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("portal_meal_plan_not_found", (await ReadJsonAsync(response)).GetProperty("title").GetString());
        Assert.Equal(before, await PortalTestData.GetSelectedMealPlanIdAsync(
            _fixture.Factory, trainerId, clientUserId, Token));
    }

    [Fact]
    public async Task SelectMyMealPlan_PlanOfAnotherTrainer_ReturnsNotFound()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var (otherTrainerId, otherUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var foreignPlanId = await PortalTestData.SeedExtraMealPlanAsync(
            _fixture.Factory, otherTrainerId, otherUserId, "Plano de outro tenant", Token);

        var response = await ApiJsonPayload.PutAsync(
            PortalClient(trainerId, clientUserId),
            "/api/v1/portal/my-meal-plans/selection",
            new SelectMyMealPlanRequest(foreignPlanId),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ArchivingSelectedPlan_ClearsSelectionAndReactivatingDoesNotRestoreIt()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        await PortalTestData.SeedExtraMealPlanAsync(
            _fixture.Factory, trainerId, clientUserId, "Dias de descanso", Token);
        var selectedId = (await PortalTestData.GetSelectedMealPlanIdAsync(
            _fixture.Factory, trainerId, clientUserId, Token))!.Value;
        var trainer = _fixture.Factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueTrainer(trainerId));
        var portal = PortalClient(trainerId, clientUserId);

        var archive = await trainer.PostAsync($"/api/v1/meal-plans/{selectedId}/archive", null, Token);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        Assert.Null(await PortalTestData.GetSelectedMealPlanIdAsync(_fixture.Factory, trainerId, clientUserId, Token));
        var nutrition = await portal.GetAsync("/api/v1/portal/my-nutrition", Token);
        Assert.Equal(HttpStatusCode.NotFound, nutrition.StatusCode);
        Assert.Equal(
            "portal_nutrition_plan_not_selected",
            (await ReadJsonAsync(nutrition)).GetProperty("title").GetString());

        var archived = await ApiJsonPayload.PutAsync(
            portal, "/api/v1/portal/my-meal-plans/selection", new SelectMyMealPlanRequest(selectedId), Token);
        Assert.Equal(HttpStatusCode.NotFound, archived.StatusCode);

        var reactivate = await trainer.PostAsync($"/api/v1/meal-plans/{selectedId}/reactivate", null, Token);
        Assert.Equal(HttpStatusCode.NoContent, reactivate.StatusCode);
        Assert.Null(await PortalTestData.GetSelectedMealPlanIdAsync(_fixture.Factory, trainerId, clientUserId, Token));
    }

    [Fact]
    public async Task ReactivatingPlan_ClearsAChoiceLeftByARaceWithArchiving()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var planId = (await PortalTestData.GetSelectedMealPlanIdAsync(
            _fixture.Factory, trainerId, clientUserId, Token))!.Value;
        var trainer = _fixture.Factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueTrainer(trainerId));
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await trainer.PostAsync($"/api/v1/meal-plans/{planId}/archive", null, Token)).StatusCode);
        // Resultado de uma escolha que correu em paralelo com o arquivo: a ficha ficou a apontar
        // para o plano já arquivado.
        await PortalTestData.ForceMealPlanSelectionAsync(_fixture.Factory, trainerId, clientUserId, planId, Token);

        var reactivate = await trainer.PostAsync($"/api/v1/meal-plans/{planId}/reactivate", null, Token);

        Assert.Equal(HttpStatusCode.NoContent, reactivate.StatusCode);
        Assert.Null(await PortalTestData.GetSelectedMealPlanIdAsync(_fixture.Factory, trainerId, clientUserId, Token));
    }

    [Fact]
    public async Task MyNutrition_SinglePlanWithoutChoice_NeverOpensItAutomatically()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        await PortalTestData.ClearMealPlanSelectionAsync(_fixture.Factory, trainerId, clientUserId, Token);
        var portal = PortalClient(trainerId, clientUserId);

        var nutrition = await portal.GetAsync("/api/v1/portal/my-nutrition", Token);
        var home = await portal.GetAsync("/api/v1/portal/home", Token);

        Assert.Equal(HttpStatusCode.NotFound, nutrition.StatusCode);
        Assert.Equal(
            "portal_nutrition_plan_not_selected",
            (await ReadJsonAsync(nutrition)).GetProperty("title").GetString());
        var homeBody = await ReadJsonAsync(home);
        Assert.Equal(JsonValueKind.Null, homeBody.GetProperty("nutrition").ValueKind);
        Assert.Equal(1, homeBody.GetProperty("active_meal_plan_count").GetInt32());
    }

    [Fact]
    public async Task MyWorkoutDays_ListsOnlyPastTuesdaysOfTheActivePlan()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);

        var response = await PortalClient(trainerId, clientUserId).GetAsync("/api/v1/portal/my-workout/days", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dates = (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray()
            .Select(item => DateOnly.Parse(item.GetProperty("date").GetString()!, System.Globalization.CultureInfo.InvariantCulture))
            .ToList();
        Assert.NotEmpty(dates);
        Assert.All(dates, date => Assert.Equal(DayOfWeek.Tuesday, date.DayOfWeek));
        Assert.All(dates, date => Assert.True(date < TodayInLisbon));
        Assert.All(dates, date => Assert.True(date >= new DateOnly(2026, 9, 2)));
        Assert.Equal(dates.OrderByDescending(date => date).ToList(), dates);
    }

    [Fact]
    public async Task MyWorkoutDay_PastDay_ReturnsLogsAndNotesOnlyForReading()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var portal = PortalClient(trainerId, clientUserId);
        var days = await ReadJsonAsync(await portal.GetAsync("/api/v1/portal/my-workout/days", Token));
        var date = days.GetProperty("items")[0].GetProperty("date").GetString()!;
        await PortalTestData.SeedPastWorkoutAsync(
            _fixture.Factory,
            trainerId,
            clientUserId,
            DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture),
            "Joelho ok",
            "Treino forte",
            Token);

        var response = await portal.GetAsync($"/api/v1/portal/my-workout/day?date={date}", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("workout", body.GetProperty("status").GetString());
        Assert.Equal(date, body.GetProperty("local_date").GetString());
        Assert.Equal("Treino forte", body.GetProperty("completion_notes").GetString());
        var logged = body.GetProperty("day").GetProperty("exercises")[0].GetProperty("sets")[0].GetProperty("logged");
        Assert.Equal("Joelho ok", logged.GetProperty("notes").GetString());
        Assert.Equal(9, logged.GetProperty("reps_done").GetInt32());
    }

    [Fact]
    public async Task MyWorkoutDay_TodayFutureOrBeforePlan_ReturnsBadRequest()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var portal = PortalClient(trainerId, clientUserId);
        string[] dates =
        [
            TodayInLisbon.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            TodayInLisbon.AddDays(7).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            "2026-09-01"
        ];

        foreach (var date in dates)
        {
            var response = await portal.GetAsync($"/api/v1/portal/my-workout/day?date={date}", Token);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var error = Assert.Single((await ReadJsonAsync(response)).GetProperty("errors").EnumerateArray());
            Assert.Equal("portal_workout_day_not_available", error.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task MyWorkoutDay_OtherClientLogs_AreNeverVisible()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var (_, otherUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token, trainerId);
        await PortalTestData.SeedTrainingPlanForClientAsync(_fixture.Factory, trainerId, otherUserId, Token);
        var portal = PortalClient(trainerId, clientUserId);
        var date = (await ReadJsonAsync(await portal.GetAsync("/api/v1/portal/my-workout/days", Token)))
            .GetProperty("items")[0].GetProperty("date").GetString()!;
        await PortalTestData.SeedPastWorkoutAsync(
            _fixture.Factory,
            trainerId,
            otherUserId,
            DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture),
            "Nota do outro",
            "Conclusão do outro",
            Token);

        var body = await ReadJsonAsync(await portal.GetAsync($"/api/v1/portal/my-workout/day?date={date}", Token));

        Assert.Equal(JsonValueKind.Null, body.GetProperty("completion_notes").ValueKind);
        var set = body.GetProperty("day").GetProperty("exercises")[0].GetProperty("sets")[0];
        Assert.Equal(JsonValueKind.Null, set.GetProperty("logged").ValueKind);
    }

    [Fact]
    public async Task NewPortalEndpoints_WithTrainerToken_ReturnForbidden()
    {
        var (trainerId, _) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var trainer = _fixture.Factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueTrainer(trainerId));

        Assert.Equal(HttpStatusCode.Forbidden, (await trainer.GetAsync("/api/v1/portal/my-meal-plans", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await trainer.GetAsync("/api/v1/portal/my-workout/days", Token)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await trainer.GetAsync("/api/v1/portal/my-workout/day?date=2026-09-08", Token)).StatusCode);
    }

    private HttpClient PortalClient(Guid trainerId, Guid clientUserId) =>
        _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId));

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ApiJsonPayload.Options, Token);
}
