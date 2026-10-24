using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.FunctionalTests.Support;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Item de catálogo bloqueado pela moderação dentro de um plano (RN-013): sai do portal do
/// cliente e das kcal e macros, fica marcado com o motivo e a nota no plano do personal trainer
/// (também fora dos totais) e, ao desbloquear, sai do plano. Contra PostgreSQL real.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class BlockedCatalogItemTests
{
    private const string Moderation = "/api/v1/admin/content-moderation";

    private readonly PostgresApiFixture _fixture;

    public BlockedCatalogItemTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task BlockedSupplement_LeavesThePortal_CannotBeMarked_AndTheTrainerSeesReasonAndNote()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var ids = await ReadIdsAsync(trainerId, clientUserId);
        var admin = await AdminAsync();
        var portal = PortalClient(trainerId, clientUserId);

        var blockMeal = await BlockAsync(admin, "supplements", ids.MealSupplementId, "Rótulo enganador.");
        var blockDirect = await BlockAsync(admin, "supplements", ids.DirectSupplementId, null);

        var nutrition = await ReadJsonAsync(await portal.GetAsync("/api/v1/portal/my-nutrition", Token));
        var today = await ReadJsonAsync(await portal.GetAsync("/api/v1/portal/my-supplements/intakes/today", Token));
        var mark = await portal.PutAsync($"/api/v1/portal/my-supplements/{ids.AssignmentId}/intakes/today", null, Token);
        var plan = await ReadJsonAsync(await TrainerClient(trainerId).GetAsync($"/api/v1/meal-plans/{ids.MealPlanId}", Token));
        var queue = await ReadJsonAsync(await admin.GetAsync($"{Moderation}/supplements?status=blocked&search=Portal%20whey", Token));

        Assert.Equal(HttpStatusCode.NoContent, blockMeal.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, blockDirect.StatusCode);
        Assert.Empty(nutrition.GetProperty("meals")[0].GetProperty("supplements").EnumerateArray());
        Assert.Empty(today.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, mark.StatusCode);

        var marked = plan.GetProperty("meals")[0].GetProperty("supplements")[0].GetProperty("platform_enforcement");
        Assert.Equal("malicious_content", marked.GetProperty("reason").GetString());
        Assert.Equal("Rótulo enganador.", marked.GetProperty("note").GetString());
        Assert.True(plan.GetProperty("needs_review").GetBoolean());
        Assert.Contains(queue.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("id").GetGuid() == ids.MealSupplementId &&
            item.GetProperty("platform_enforcement_note").GetString() == "Rótulo enganador.");
    }

    [Fact]
    public async Task BlockedFood_LeavesThePortalAndEveryTotal_ButStaysMarkedInTheTrainersPlan()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var ids = await ReadIdsAsync(trainerId, clientUserId);
        var portal = PortalClient(trainerId, clientUserId);
        var before = await ReadJsonAsync(await portal.GetAsync("/api/v1/portal/my-nutrition", Token));

        await BlockAsync(await AdminAsync(), "foods", ids.FoodId, "Valores inventados.");
        var after = await ReadJsonAsync(await portal.GetAsync("/api/v1/portal/my-nutrition", Token));
        var plan = await ReadJsonAsync(await TrainerClient(trainerId).GetAsync($"/api/v1/meal-plans/{ids.MealPlanId}", Token));

        Assert.True(before.GetProperty("actual_totals").GetProperty("kcal").GetDecimal() > 0);
        Assert.Empty(after.GetProperty("meals")[0].GetProperty("items").EnumerateArray());
        Assert.Equal(0m, after.GetProperty("actual_totals").GetProperty("kcal").GetDecimal());

        var item = plan.GetProperty("meals")[0].GetProperty("items")[0];
        Assert.Equal("Valores inventados.", item.GetProperty("platform_enforcement").GetProperty("note").GetString());
        Assert.Equal(0m, plan.GetProperty("meals")[0].GetProperty("totals").GetProperty("kcal").GetDecimal());
        Assert.Equal(0m, plan.GetProperty("actual_totals").GetProperty("kcal").GetDecimal());
    }

    [Fact]
    public async Task UnblockingAFood_TakesItOutOfThePlan_AndKeepsItInTheLibrary()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var ids = await ReadIdsAsync(trainerId, clientUserId);
        var admin = await AdminAsync();
        await BlockAsync(admin, "foods", ids.FoodId, null);

        var unblock = await admin.PostAsync($"{Moderation}/foods/{ids.FoodId}/unblock", null, Token);
        var plan = await ReadJsonAsync(await TrainerClient(trainerId).GetAsync($"/api/v1/meal-plans/{ids.MealPlanId}", Token));
        var food = await TrainerClient(trainerId).GetAsync($"/api/v1/foods/{ids.FoodId}", Token);

        Assert.Equal(HttpStatusCode.NoContent, unblock.StatusCode);
        Assert.Empty(plan.GetProperty("meals")[0].GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, food.StatusCode);
        Assert.Equal("allowed", (await ReadJsonAsync(food)).GetProperty("platform_enforcement_status").GetString());
    }

    [Fact]
    public async Task UnblockingAnExerciseWithLoggedSets_RetiresIt_HiddenFromBothSides_AndNoMoreSetsCanBeLogged()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        await PortalTestData.SeedPastWorkoutAsync(
            _fixture.Factory, trainerId, clientUserId, new DateOnly(2026, 9, 1), "ok", "ok", Token);
        var ids = await ReadIdsAsync(trainerId, clientUserId);
        var admin = await AdminAsync();
        await BlockAsync(admin, "exercises", ids.ExerciseId, null);

        var unblock = await admin.PostAsync($"{Moderation}/exercises/{ids.ExerciseId}/unblock", null, Token);
        var myPlan = await ReadJsonAsync(await PortalClient(trainerId, clientUserId).GetAsync("/api/v1/portal/my-plan", Token));
        var trainerPlan = await ReadJsonAsync(await TrainerClient(trainerId)
            .GetAsync($"/api/v1/training-plans/{ids.TrainingPlanId}", Token));
        var log = await ApiJsonPayload.PostAsync(
            PortalClient(trainerId, clientUserId),
            "/api/v1/portal/exercise-set-logs",
            new { training_plan_day_exercise_id = ids.PrescriptionId, set_number = 1, weight_kg = 60m, reps_done = 8 },
            Token);

        Assert.Equal(HttpStatusCode.NoContent, unblock.StatusCode);
        Assert.NotNull(await RetiredAtAsync(trainerId, ids.PrescriptionId));
        Assert.Equal(1, await LogCountAsync(trainerId, ids.PrescriptionId));
        Assert.Empty(myPlan.GetProperty("days")[0].GetProperty("exercises").EnumerateArray());
        Assert.Empty(trainerPlan.GetProperty("days")[0].GetProperty("exercises").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, log.StatusCode);
    }

    [Fact]
    public async Task BlockingAGlobalSupplement_IsRefusedWithoutA500()
    {
        var supplementId = await GlobalSupplementIdAsync();
        if (supplementId is null)
            return;

        var response = await BlockAsync(await AdminAsync(), "supplements", supplementId.Value, null);

        Assert.True((int)response.StatusCode is >= 400 and < 500, response.StatusCode.ToString());
    }

    private async Task<HttpResponseMessage> BlockAsync(HttpClient admin, string kind, Guid id, string? note) =>
        await ApiJsonPayload.PostAsync(
            admin, $"{Moderation}/{kind}/{id}/block", new { reason_code = "malicious_content", note }, Token);

    private async Task<PlanIds> ReadIdsAsync(Guid trainerId, Guid clientUserId)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var clientId = await db.Clients.IgnoreQueryFilters()
            .Where(item => item.OwnerTrainerId == trainerId && item.UserId == clientUserId)
            .Select(item => item.Id).SingleAsync(Token);
        var mealPlanId = await db.MealPlans.IgnoreQueryFilters()
            .Where(item => item.ClientId == clientId).Select(item => item.Id).FirstAsync(Token);
        var meal = await db.MealPlanMeals.IgnoreQueryFilters()
            .Where(item => item.MealPlanId == mealPlanId).Select(item => item.Id).FirstAsync(Token);
        var foodId = await db.MealPlanMealItems.IgnoreQueryFilters()
            .Where(item => item.MealPlanMealId == meal).Select(item => item.FoodId).FirstAsync(Token);
        var mealSupplementId = await db.MealPlanMealSupplements.IgnoreQueryFilters()
            .Where(item => item.MealPlanMealId == meal).Select(item => item.SupplementId).FirstAsync(Token);
        var assignment = await db.ClientSupplementAssignments.IgnoreQueryFilters()
            .Where(item => item.ClientId == clientId)
            .Select(item => new { item.Id, item.SupplementId }).FirstAsync(Token);
        var training = await (
                from plan in db.TrainingPlans.IgnoreQueryFilters()
                join day in db.TrainingPlanDays.IgnoreQueryFilters() on plan.Id equals day.TrainingPlanId
                join item in db.TrainingPlanDayExercises.IgnoreQueryFilters() on day.Id equals item.TrainingPlanDayId
                where plan.ClientId == clientId
                select new { PlanId = plan.Id, item.Id, item.ExerciseId })
            .FirstAsync(Token);

        return new PlanIds(
            mealPlanId, foodId, mealSupplementId, assignment.SupplementId, assignment.Id,
            training.PlanId, training.Id, training.ExerciseId);
    }

    private async Task<DateTime?> RetiredAtAsync(Guid trainerId, Guid prescriptionId)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PtManagerDbContext>().TrainingPlanDayExercises
            .IgnoreQueryFilters()
            .Where(item => item.Id == prescriptionId)
            .Select(item => item.RetiredAt)
            .SingleAsync(Token);
    }

    private async Task<int> LogCountAsync(Guid trainerId, Guid prescriptionId)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PtManagerDbContext>().ClientExerciseSetLogs
            .IgnoreQueryFilters()
            .CountAsync(log => log.TrainingPlanDayExerciseId == prescriptionId, Token);
    }

    private async Task<Guid?> GlobalSupplementIdAsync()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PtManagerDbContext>().Supplements
            .IgnoreQueryFilters()
            .Where(item => item.OwnerTrainerId == null)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(Token);
    }

    private async Task<HttpClient> AdminAsync()
    {
        var id = await AuthTestData.SeedAccountAsync(
            _fixture.Factory, $"admin-{Guid.NewGuid():N}@moderation.test", role: "superuser");
        return _fixture.Factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueSuperuser(id));
    }

    private HttpClient TrainerClient(Guid trainerId) =>
        _fixture.Factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueTrainer(trainerId));

    private HttpClient PortalClient(Guid trainerId, Guid clientUserId) =>
        _fixture.Factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId));

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ApiJsonPayload.Options, Token);

    private sealed record PlanIds(
        Guid MealPlanId,
        Guid FoodId,
        Guid MealSupplementId,
        Guid DirectSupplementId,
        Guid AssignmentId,
        Guid TrainingPlanId,
        Guid PrescriptionId,
        Guid ExerciseId);
}
