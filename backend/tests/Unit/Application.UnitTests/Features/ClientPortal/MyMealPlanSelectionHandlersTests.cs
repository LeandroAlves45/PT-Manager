using Application.Features.ClientPortal;
using Application.Features.ClientPortal.Abstractions;
using Application.Features.ClientPortal.GetMyNutritionPlan;
using Application.Features.ClientPortal.SelectMyMealPlan;

namespace Application.UnitTests.Features;

/// <summary>
/// Escolha do plano alimentar no portal: a app nunca escolhe pelo cliente, mesmo com um só
/// plano, e distingue "falta escolher" de "não há planos".
/// </summary>
public sealed class MyMealPlanSelectionHandlersTests : ReadHandlerTestContext
{
    private static GetMyNutritionPlanHandler NutritionHandler(NutritionQueriesFake queries) =>
        new(new TenantStub(TrainerId, ClientUserId, "client"), queries);

    [Fact]
    public async Task Nutrition_WithSelection_ReturnsSelectedPlan()
    {
        var result = await NutritionHandler(new NutritionQueriesFake())
            .HandleAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("maintenance", result.Value.GoalType);
    }

    [Fact]
    public async Task Nutrition_OnePlanWithoutSelection_AsksToChoose()
    {
        var queries = new NutritionQueriesFake { HasSelection = false, ActiveCount = 1 };

        var result = await NutritionHandler(queries).HandleAsync(CancellationToken.None);

        Assert.Equal(ClientPortalErrors.NutritionPlanNotSelected.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Nutrition_WithoutPlans_IsNotAvailable()
    {
        var queries = new NutritionQueriesFake { HasSelection = false, ActiveCount = 0 };

        var result = await NutritionHandler(queries).HandleAsync(CancellationToken.None);

        Assert.Equal(ClientPortalErrors.NutritionPlanNotAvailable.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Select_StoreRejects_ReturnsNotFound()
    {
        var store = new SelectionStoreFake { Accepts = false };
        var handler = new SelectMyMealPlanHandler(new TenantStub(TrainerId, ClientUserId, "client"), store);

        var result = await handler.HandleAsync(
            new SelectMyMealPlanCommand(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(ClientPortalErrors.MealPlanNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Select_EmptyId_NeverReachesStore()
    {
        var store = new SelectionStoreFake();
        var handler = new SelectMyMealPlanHandler(new TenantStub(TrainerId, ClientUserId, "client"), store);

        var result = await handler.HandleAsync(new SelectMyMealPlanCommand(Guid.Empty), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task Select_UsesAuthenticatedClient()
    {
        var store = new SelectionStoreFake();
        var planId = Guid.NewGuid();
        var handler = new SelectMyMealPlanHandler(new TenantStub(TrainerId, ClientUserId, "client"), store);

        var result = await handler.HandleAsync(new SelectMyMealPlanCommand(planId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((TrainerId, ClientUserId, planId), store.Last);
    }

    [Fact]
    public async Task Select_TrainerActor_IsForbidden()
    {
        var store = new SelectionStoreFake();
        var handler = new SelectMyMealPlanHandler(new TenantStub(TrainerId, TrainerId, "trainer"), store);

        var result = await handler.HandleAsync(new SelectMyMealPlanCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ClientPortalErrors.ClientOnly.Code, result.Error!.Code);
        Assert.Equal(0, store.Calls);
    }

    private sealed class SelectionStoreFake : IMyMealPlanSelectionStore
    {
        public bool Accepts { get; init; } = true;

        public int Calls { get; private set; }

        public (Guid TrainerId, Guid UserId, Guid PlanId) Last { get; private set; }

        public Task<bool> SelectAsync(
            Guid trainerId,
            Guid clientUserId,
            Guid mealPlanId,
            CancellationToken cancellationToken)
        {
            Calls++;
            Last = (trainerId, clientUserId, mealPlanId);
            return Task.FromResult(Accepts);
        }
    }
}
