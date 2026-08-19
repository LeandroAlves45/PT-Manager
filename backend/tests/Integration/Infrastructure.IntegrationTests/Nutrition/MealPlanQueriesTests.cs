
using Application.Features.Nutrition.MealPlans.ListMealPlans;
using Application.Pagination;
using Domain.Entities.Nutrition;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Nutrition;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Nutrition;

[Collection(PostgresCollection.Name)]
public sealed class MealPlanQueriesTests
{
    private static readonly DateTime Now =
        new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);
    private readonly PostgresContainerFixture _fixture;

    public MealPlanQueriesTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetDetailsAsync_ExistingPlan_ReturnsRoundedEffectiveTotals()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await NutritionPlanTestData.SeedPlanAsync(_fixture, token);
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var details = await new MealPlanQueries(context).GetDetailsAsync(seed.MealPlanId, token);

        Assert.NotNull(details);
        var item = Assert.Single(Assert.Single(details.Meals).Items);
        Assert.Equal(3.40m, item.Contribution.ProteinGrams);
        Assert.Equal(35.28m, item.Contribution.CarbsGrams);
        Assert.Equal(0.38m, item.Contribution.FatsGrams);
        Assert.Equal(158.13m, item.Contribution.Kcal);
        Assert.Equal(0.50m, item.Contribution.FiberGrams);
        Assert.Equal(item.Contribution, details.ActualTotals);
    }

    [Fact]
    public async Task GetDetailsAsync_FoodWithoutFiber_TreatsFiberAsZero()
    {
        var token = TestContext.Current.CancellationToken;
        var catalog = await NutritionPlanTestData.SeedCatalogAsync(_fixture, token);
        Guid planId;
        await using (var setup = _fixture.CreateContext(catalog.TrainerId))
        {
            var food = new Food(
                catalog.TrainerId,
                "Fiber unknown",
                null,
                10m,
                20m,
                5m,
                null,
                Now
            );
            setup.Foods.Add(food);
            await setup.SaveChangesAsync(token);
            var created = await new MealPlanStore(setup).CreateAsync(
                catalog.TrainerId,
                NutritionPlanTestData.CreateModel(
                    catalog.ClientId,
                    food.Id,
                    catalog.SupplementId
                ),
                Now,
                token
            );
            planId = created.MealPlanId!.Value;
        }

        await using var context = _fixture.CreateContext(catalog.TrainerId);
        var details = await new MealPlanQueries(context).GetDetailsAsync(planId, token);

        Assert.NotNull(details);
        var item = Assert.Single(Assert.Single(details.Meals).Items);
        Assert.Null(item.FiberPer100G);
        Assert.Equal(0m, item.Contribution.FiberGrams);
        Assert.Equal(0m, details.ActualTotals.FiberGrams);
    }

    [Fact]
    public async Task GetDetailsAsync_ArchivedCatalogReferences_RemainsReadable()
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
        var details = await new MealPlanQueries(context).GetDetailsAsync(seed.MealPlanId, token);

        Assert.NotNull(details);
        Assert.Single(Assert.Single(details.Meals).Items);
        Assert.Single(Assert.Single(details.Meals).Supplements);
    }

    [Fact]
    public async Task GetDetailsAsync_PlanFromAnotherTenant_ReturnsNull()
    {
        var token = TestContext.Current.CancellationToken;
        var owner = await NutritionPlanTestData.SeedPlanAsync(_fixture, token);
        var requester = await _fixture.SeedTenantWithClientAsync(
            $"meal-query-requester-{Guid.NewGuid():N}",
            token
        );
        await using var context = _fixture.CreateContext(requester.TrainerId);

        var details = await new MealPlanQueries(context).GetDetailsAsync(owner.MealPlanId, token);

        Assert.Null(details);
    }

    [Fact]
    public async Task ListAsync_ReturnsStablePageWithoutTrackingAggregateTree()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await NutritionPlanTestData.SeedPlanAsync(_fixture, token);
        await using (var setup = _fixture.CreateContext(seed.TrainerId))
        {
            var store = new MealPlanStore(setup);
            for (var index = 1; index <= 2; index++)
            {
                var model = NutritionPlanTestData.CreateModel(
                    seed.ClientId,
                    seed.FoodId,
                    seed.SupplementId
                ) with
                {
                    Name = $"Nutrition plan {index}",
                    StartsDate = new DateOnly(2026, 8, 10).AddDays(index)
                };
                await store.CreateAsync(
                    seed.TrainerId,
                    model,
                    Now.AddMinutes(index),
                    token
                );
            }
        }

        await using var context = _fixture.CreateContext(seed.TrainerId);
        var queries = new MealPlanQueries(context);
        var firstPage = await queries.ListAsync(
            seed.ClientId,
            "Nutrition plan",
            MealPlanActivityFilter.Active,
            new PageRequest(1, 2),
            token
        );
        var repeatedPage = await queries.ListAsync(
            seed.ClientId,
            "Nutrition plan",
            MealPlanActivityFilter.Active,
            new PageRequest(1, 2),
            token
        );

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(
            firstPage.Items.Select(item => item.Id),
            repeatedPage.Items.Select(item => item.Id)
        );
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ListAsync_ForeignClientFilter_ReturnsEmptyPage()
    {
        var token = TestContext.Current.CancellationToken;
        var owner = await NutritionPlanTestData.SeedPlanAsync(_fixture, token);
        var foreign = await _fixture.SeedTenantWithClientAsync(
            $"meal-query-foreign-{Guid.NewGuid():N}",
            token
        );
        await using var context = _fixture.CreateContext(owner.TrainerId);

        var page = await new MealPlanQueries(context).ListAsync(
            foreign.ClientId,
            null,
            MealPlanActivityFilter.All,
            new PageRequest(),
            token
        );

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }
}
