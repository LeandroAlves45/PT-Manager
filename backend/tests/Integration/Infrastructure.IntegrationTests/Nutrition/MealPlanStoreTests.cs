
using Application.Features.Nutrition.MealPlans;
using Application.Features.Nutrition.MealPlans.Abstractions;
using Domain.Exceptions;
using Domain.Services;
using Domain.ValueObjects;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Nutrition;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Nutrition;

[Collection(PostgresCollection.Name)]
public sealed class MealPlanStoreTests
{
    private static readonly DateTime Now =
        new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);
    private readonly PostgresContainerFixture _fixture;

    public MealPlanStoreTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CreateAsync_ValidTree_PersistsCompleteAggregate()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await NutritionPlanTestData.SeedCatalogAsync(_fixture, token);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = new MealPlanStore(context);

        var result = await store.CreateAsync(
            seed.TrainerId,
            NutritionPlanTestData.CreateModel(seed.ClientId, seed.FoodId, seed.SupplementId),
            Now,
            token
        );

        Assert.Equal(MealPlanStoreResult.Status.Created, result.Kind);
        var plan = await context.MealPlans.AsNoTracking()
            .Include(candidate => candidate.Meals)
                .ThenInclude(meal => meal.Items)
            .Include(candidate => candidate.Meals)
                .ThenInclude(meal => meal.Supplements)
            .AsSplitQuery()
            .SingleAsync(candidate => candidate.Id == result.MealPlanId, token);
        var meal = Assert.Single(plan.Meals);
        Assert.Single(meal.Items);
        Assert.Single(meal.Supplements);
    }

    [Fact]
    public async Task CreateAsync_ClientFromAnotherTenant_RollsBackWholeWrite()
    {
        var token = TestContext.Current.CancellationToken;
        var owner = await NutritionPlanTestData.SeedCatalogAsync(_fixture, token);
        var foreign = await _fixture.SeedTenantWithClientAsync(
            $"meal-plan-foreign-{Guid.NewGuid():N}",
            token
        );
        await using var context = _fixture.CreateContext(owner.TrainerId);
        var store = new MealPlanStore(context);

        var result = await store.CreateAsync(
            owner.TrainerId,
            NutritionPlanTestData.CreateModel(
                foreign.ClientId,
                owner.FoodId,
                owner.SupplementId
            ),
            Now,
            token
        );

        Assert.Equal(MealPlanStoreResult.Status.ClientNotFound, result.Kind);
        Assert.False(
            await context.MealPlans.AsNoTracking()
                .AnyAsync(plan => plan.ClientId == foreign.ClientId, token)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateAsync_InvalidCatalogReference_RollsBackWholeWrite(
        bool existingButInactive
    )
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await NutritionPlanTestData.SeedCatalogAsync(_fixture, token);
        var foodId = Guid.NewGuid();
        if (existingButInactive)
        {
            await using var setup = _fixture.CreateContext(seed.TrainerId);
            var food = IntegrationTestData.Food(seed.TrainerId, Now);
            food.SetActive(false, Now);
            foodId = food.Id;
            setup.Foods.Add(food);
            await setup.SaveChangesAsync(token);
        }

        await using var context = _fixture.CreateContext(seed.TrainerId);
        var result = await new MealPlanStore(context).CreateAsync(
            seed.TrainerId,
            NutritionPlanTestData.CreateModel(seed.ClientId, foodId, seed.SupplementId),
            Now,
            token
        );

        Assert.Equal(
            existingButInactive
                ? MealPlanStoreResult.Status.CatalogReferenceInactive
                : MealPlanStoreResult.Status.CatalogReferenceNotFound,
            result.Kind
        );
        Assert.False(await context.MealPlans.AsNoTracking().AnyAsync(token));
    }

    [Fact]
    public async Task UpdateAsync_RemovedMeal_DeletesItsWholeSubtree()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await NutritionPlanTestData.SeedPlanAsync(_fixture, token, mealCount: 2);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var original = await NutritionPlanTestData.ReadStructureAsync(
            context,
            seed.MealPlanId,
            token
        );
        var keptMeal = original.Meals.OrderBy(meal => meal.OrderNumber).First();

        var result = await new MealPlanStore(context).UpdateAsync(
            seed.TrainerId,
            NutritionPlanTestData.UpdateModel(
                seed.MealPlanId,
                new MealPlanStructureInput([keptMeal with { OrderNumber = 1 }])
            ),
            Now.AddMinutes(1),
            token
        );

        Assert.Equal(MealPlanStoreResult.Status.Updated, result.Kind);
        context.ChangeTracker.Clear();
        var remainingMealIds = await context.MealPlanMeals.AsNoTracking()
            .Where(meal => meal.MealPlanId == seed.MealPlanId)
            .Select(meal => meal.Id)
            .ToArrayAsync(token);
        Assert.Equal(
            1,
            await context.MealPlanMeals.CountAsync(
                meal => meal.MealPlanId == seed.MealPlanId,
                token
            )
        );
        Assert.Equal(
            1,
            await context.MealPlanMealItems.CountAsync(
                item => remainingMealIds.Contains(item.MealPlanMealId),
                token
            )
        );
    }

    [Fact]
    public async Task UpdateAsync_SwappedOrders_SucceedsWithoutTransientCollision()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await NutritionPlanTestData.SeedPlanAsync(_fixture, token, mealCount: 2);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var structure = await NutritionPlanTestData.ReadStructureAsync(
            context,
            seed.MealPlanId,
            token
        );
        var first = structure.Meals.Single(meal => meal.OrderNumber == 1);
        var second = structure.Meals.Single(meal => meal.OrderNumber == 2);
        var swapped = new MealPlanStructureInput(
            [first with { OrderNumber = 2 }, second with { OrderNumber = 1 }]
        );

        var result = await new MealPlanStore(context).UpdateAsync(
            seed.TrainerId,
            NutritionPlanTestData.UpdateModel(seed.MealPlanId, swapped),
            Now.AddMinutes(1),
            token
        );

        Assert.Equal(MealPlanStoreResult.Status.Updated, result.Kind);
        context.ChangeTracker.Clear();
        var orders = await context.MealPlanMeals.AsNoTracking()
            .Where(meal => meal.MealPlanId == seed.MealPlanId)
            .OrderBy(meal => meal.OrderNumber)
            .Select(meal => meal.Id)
            .ToArrayAsync(token);
        Assert.Equal([second.Id!.Value, first.Id!.Value], orders);
    }

    [Fact]
    public async Task UpdateAsync_InvalidFinalOrders_RollsBackTemporaryOrderStage()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await NutritionPlanTestData.SeedPlanAsync(_fixture, token, mealCount: 2);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var structure = await NutritionPlanTestData.ReadStructureAsync(
            context,
            seed.MealPlanId,
            token
        );
        var invalid = new MealPlanStructureInput(
            structure.Meals.Select(meal => meal with { OrderNumber = 1 }).ToArray()
        );

        await Assert.ThrowsAsync<DomainException>(() =>
            new MealPlanStore(context).UpdateAsync(
                seed.TrainerId,
                NutritionPlanTestData.UpdateModel(seed.MealPlanId, invalid),
                Now.AddMinutes(1),
                token
            )
        );

        await using var verify = _fixture.CreateContext(seed.TrainerId);
        var persistedOrders = await verify.MealPlanMeals.AsNoTracking()
            .Where(meal => meal.MealPlanId == seed.MealPlanId)
            .OrderBy(meal => meal.OrderNumber)
            .Select(meal => meal.OrderNumber)
            .ToArrayAsync(token);
        Assert.Equal([1, 2], persistedOrders);
    }

    [Fact]
    public async Task UpdateAsync_TwoConcurrentReconciliations_BothCompleteUnderRootLock()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await NutritionPlanTestData.SeedPlanAsync(_fixture, token, mealCount: 2);
        await using var readContext = _fixture.CreateContext(seed.TrainerId);
        var structure = await NutritionPlanTestData.ReadStructureAsync(
            readContext,
            seed.MealPlanId,
            token
        );
        var reversed = new MealPlanStructureInput(
            structure.Meals
                .OrderByDescending(meal => meal.OrderNumber)
                .Select((meal, index) => meal with { OrderNumber = index + 1 })
                .ToArray()
        );
        await using var firstContext = _fixture.CreateContext(seed.TrainerId);
        await using var secondContext = _fixture.CreateContext(seed.TrainerId);

        var results = await Task.WhenAll(
            new MealPlanStore(firstContext).UpdateAsync(
                seed.TrainerId,
                NutritionPlanTestData.UpdateModel(seed.MealPlanId, reversed, "First update"),
                Now.AddMinutes(1),
                token
            ),
            new MealPlanStore(secondContext).UpdateAsync(
                seed.TrainerId,
                NutritionPlanTestData.UpdateModel(seed.MealPlanId, structure, "Second update"),
                Now.AddMinutes(2),
                token
            )
        );

        Assert.All(
            results,
            result => Assert.Equal(MealPlanStoreResult.Status.Updated, result.Kind)
        );
    }

    [Fact]
    public async Task UpdateAsync_UnknownNestedId_RollsBackMetadataAndTree()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await NutritionPlanTestData.SeedPlanAsync(_fixture, token);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var structure = await NutritionPlanTestData.ReadStructureAsync(
            context,
            seed.MealPlanId,
            token
        );
        var meal = Assert.Single(structure.Meals);
        var invalidItem = Assert.Single(meal.Items) with { Id = Guid.NewGuid() };
        var invalid = new MealPlanStructureInput([meal with { Items = [invalidItem] }]);

        var result = await new MealPlanStore(context).UpdateAsync(
            seed.TrainerId,
            NutritionPlanTestData.UpdateModel(seed.MealPlanId, invalid, "Changed"),
            Now.AddMinutes(1),
            token
        );

        Assert.Equal(MealPlanStoreResult.Status.StructureReferenceNotFound, result.Kind);
        context.ChangeTracker.Clear();
        Assert.Equal(
            "Nutrition plan",
            await context.MealPlans.AsNoTracking()
                .Where(plan => plan.Id == seed.MealPlanId)
                .Select(plan => plan.Name)
                .SingleAsync(token)
        );
    }

    [Fact]
    public async Task UpdateAsync_UnchangedArchivedReferences_RemainsEditable()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await NutritionPlanTestData.SeedPlanAsync(_fixture, token);
        await using (var setup = _fixture.CreateContext(seed.TrainerId))
        {
            (await setup.Foods.SingleAsync(food => food.Id == seed.FoodId, token))
                .SetActive(false, Now);
            (await setup.Supplements.SingleAsync(item => item.Id == seed.SupplementId, token))
                .Archive(Now);
            await setup.SaveChangesAsync(token);
        }

        await using var context = _fixture.CreateContext(seed.TrainerId);
        var structure = await NutritionPlanTestData.ReadStructureAsync(
            context,
            seed.MealPlanId,
            token
        );

        var result = await new MealPlanStore(context).UpdateAsync(
            seed.TrainerId,
            NutritionPlanTestData.UpdateModel(seed.MealPlanId, structure, "Renamed"),
            Now.AddMinutes(1),
            token
        );

        Assert.Equal(MealPlanStoreResult.Status.Updated, result.Kind);
    }

    [Fact]
    public async Task SetArchivedAsync_ConcurrentSameIntent_IsIdempotent()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await NutritionPlanTestData.SeedPlanAsync(_fixture, token);
        await using var firstContext = _fixture.CreateContext(seed.TrainerId);
        await using var secondContext = _fixture.CreateContext(seed.TrainerId);

        var results = await Task.WhenAll(
            new MealPlanStore(firstContext).SetArchivedAsync(
                seed.MealPlanId,
                seed.TrainerId,
                true,
                Now,
                token
            ),
            new MealPlanStore(secondContext).SetArchivedAsync(
                seed.MealPlanId,
                seed.TrainerId,
                true,
                Now,
                token
            )
        );

        Assert.Contains(results, result => result.Kind == MealPlanStoreResult.Status.Changed);
        Assert.All(
            results,
            result => Assert.Contains(
                result.Kind,
                new[]
                {
                    MealPlanStoreResult.Status.Changed,
                    MealPlanStoreResult.Status.AlreadyInRequestedState
                }
            )
        );
    }
}

internal static class NutritionPlanTestData
{
    private static readonly DateTime Now =
        new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);

    internal sealed record CatalogSeed(
        Guid TrainerId,
        Guid ClientId,
        Guid FoodId,
        Guid SupplementId
    );

    internal sealed record PlanSeed(
        Guid TrainerId,
        Guid ClientId,
        Guid FoodId,
        Guid SupplementId,
        Guid MealPlanId
    );

    public static async Task<CatalogSeed> SeedCatalogAsync(
        PostgresContainerFixture fixture,
        CancellationToken cancellationToken
    )
    {
        var tenant = await fixture.SeedTenantWithClientAsync(
            $"nutrition-plan-{Guid.NewGuid():N}",
            cancellationToken
        );
        await using var context = fixture.CreateContext(tenant.TrainerId);
        var food = IntegrationTestData.Food(tenant.TrainerId, Now);
        var supplement = IntegrationTestData.Supplement(tenant.TrainerId, Now);
        context.Foods.Add(food);
        context.Supplements.Add(supplement);
        await context.SaveChangesAsync(cancellationToken);
        return new CatalogSeed(
            tenant.TrainerId,
            tenant.ClientId,
            food.Id,
            supplement.Id
        );
    }

    public static async Task<PlanSeed> SeedPlanAsync(
        PostgresContainerFixture fixture,
        CancellationToken cancellationToken,
        int mealCount = 1
    )
    {
        var catalog = await SeedCatalogAsync(fixture, cancellationToken);
        await using var context = fixture.CreateContext(catalog.TrainerId);
        var result = await new MealPlanStore(context).CreateAsync(
            catalog.TrainerId,
            CreateModel(catalog.ClientId, catalog.FoodId, catalog.SupplementId, mealCount),
            Now,
            cancellationToken
        );
        return new PlanSeed(
            catalog.TrainerId,
            catalog.ClientId,
            catalog.FoodId,
            catalog.SupplementId,
            result.MealPlanId!.Value
        );
    }

    public static CreateMealPlanWriteModel CreateModel(
        Guid clientId,
        Guid foodId,
        Guid supplementId,
        int mealCount = 1
    )
    {
        var meals = Enumerable.Range(1, mealCount)
            .Select(order => new MealPlanStructureInput.MealInput(
                null,
                $"Meal {order}",
                order,
                [new(null, foodId, 125m + order, 1)],
                [new(null, supplementId, null, 5m, 1)]
            ))
            .ToArray();
        return new CreateMealPlanWriteModel(
            clientId,
            "Nutrition plan",
            null,
            new DateOnly(2026, 8, 10),
            null,
            Snapshot(),
            new MealPlanStructureInput(meals)
        );
    }

    public static UpdateMealPlanWriteModel UpdateModel(
        Guid mealPlanId,
        MealPlanStructureInput structure,
        string name = "Nutrition plan"
    ) => new(
        mealPlanId,
        name,
        null,
        new DateOnly(2026, 8, 10),
        null,
        null,
        structure
    );

    public static async Task<MealPlanStructureInput> ReadStructureAsync(
        Infrastructure.Data.PtManagerDbContext context,
        Guid mealPlanId,
        CancellationToken cancellationToken
    )
    {
        var plan = await context.MealPlans.AsNoTracking()
            .Include(candidate => candidate.Meals)
                .ThenInclude(meal => meal.Items)
            .Include(candidate => candidate.Meals)
                .ThenInclude(meal => meal.Supplements)
            .AsSplitQuery()
            .SingleAsync(candidate => candidate.Id == mealPlanId, cancellationToken);
        return new MealPlanStructureInput(
            plan.Meals
                .OrderBy(meal => meal.OrderNumber)
                .Select(meal => new MealPlanStructureInput.MealInput(
                    meal.Id,
                    meal.MealType,
                    meal.OrderNumber,
                    meal.Items
                        .OrderBy(item => item.OrderNumber)
                        .Select(item => new MealPlanStructureInput.ItemInput(
                            item.Id,
                            item.FoodId,
                            item.QuantityInGrams,
                            item.OrderNumber
                        ))
                        .ToArray(),
                    meal.Supplements
                        .OrderBy(item => item.OrderNumber)
                        .Select(item => new MealPlanStructureInput.SupplementInput(
                            item.Id,
                            item.SupplementId,
                            item.Notes,
                            item.Quantity,
                            item.OrderNumber
                        ))
                        .ToArray()
                ))
                .ToArray()
        );
    }

    private static NutritionCalculationSnapshot Snapshot()
    {
        var macros = MacroTargetCalculator.CalculateFromPercentage(
            2000m,
            new PercentageMacroInput(30m, 40m, 30m)
        );
        return NutritionCalculationSnapshot.FromManualEnergy(80m, macros, Now);
    }
}
