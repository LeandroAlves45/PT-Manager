using Application.Features.Nutrition.Foods.Abstractions;
using Application.Features.Nutrition.Foods.ListFoods;
using Application.Pagination;
using Domain.Entities.Administration;
using Domain.Entities.Nutrition;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Nutrition;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Nutrition;

[Collection(PostgresCollection.Name)]
public sealed class FoodPersistenceTests
{
    private static readonly DateTime Now = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);
    private readonly PostgresContainerFixture _fixture;

    public FoodPersistenceTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Add_ReloadsDatabaseGeneratedKcal()
    {
        var token = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(Guid.NewGuid().ToString("N"), token);
        var food = new Food(tenant.TrainerId, "Rice", null, 2.7m, 28m, 0.3m, 0.4m, Now);
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        var store = new FoodStore(context);

        await store.AddAsync(food, token);
        var persisted = await store.GetOwnedForReadAsync(food.Id, token);

        Assert.NotNull(persisted);
        Assert.Equal(2.7m * 4m + 28m * 4m + 0.3m * 9m, persisted.Kcal);
    }

    [Fact]
    public async Task List_ReturnsActiveGlobalAndCurrentTenantOnly()
    {
        var token = TestContext.Current.CancellationToken;
        var owner = await _fixture.SeedTenantWithClientAsync($"owner-{Guid.NewGuid():N}", token);
        var other = await _fixture.SeedTenantWithClientAsync($"other-{Guid.NewGuid():N}", token);
        var marker = Guid.NewGuid().ToString("N");
        var globalActive = CreateFood(null, $"global-active-{marker}");
        var globalInactive = CreateFood(null, $"global-inactive-{marker}");
        globalInactive.SetActive(false, Now.AddMinutes(1));
        var actorUserId = owner.TrainerId;

        await using (var admin = _fixture.CreateAdministrativeContext(actorUserId))
        {
            admin.Foods.AddRange(globalActive, globalInactive);
            admin.AdministrativeAuditEntries.AddRange(
                CreateAudit(actorUserId, globalActive),
                CreateAudit(actorUserId, globalInactive));
            await admin.SaveChangesAsync(token);
        }

        var ownActive = CreateFood(owner.TrainerId, $"own-active-{marker}");
        var ownArchived = CreateFood(owner.TrainerId, $"own-archived-{marker}");
        ownArchived.SetActive(false, Now.AddMinutes(1));
        await using (var ownerContext = _fixture.CreateContext(owner.TrainerId))
        {
            ownerContext.Foods.AddRange(ownActive, ownArchived);
            await ownerContext.SaveChangesAsync(token);
        }

        await using (var otherContext = _fixture.CreateContext(other.TrainerId))
        {
            otherContext.Foods.Add(CreateFood(other.TrainerId, $"other-{marker}"));
            await otherContext.SaveChangesAsync(token);
        }

        await using var context = _fixture.CreateContext(owner.TrainerId);
        var result = await new FoodQueries(context).ListAsync(
            marker,
            FoodActivityFilter.All,
            new PageRequest(1, 100),
            token
        );

        Assert.Contains(result.Items, item => item.Id == globalActive.Id);
        Assert.Contains(result.Items, item => item.Id == ownActive.Id);
        Assert.Contains(result.Items, item => item.Id == ownArchived.Id);
        Assert.DoesNotContain(result.Items, item => item.Id == globalInactive.Id);
        Assert.DoesNotContain(result.Items, item => item.Name == $"other-{marker}");
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("\\")]
    public async Task List_EscapesLikeWildcards(string literal)
    {
        var token = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(Guid.NewGuid().ToString("N"), token);
        var marker = Guid.NewGuid().ToString("N");
        var expected = CreateFood(tenant.TrainerId, $"literal-{literal}-{marker}");
        var control = CreateFood(tenant.TrainerId, $"literal-x-{marker}");
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        context.Foods.AddRange(expected, control);
        await context.SaveChangesAsync(token);

        var result = await new FoodQueries(context).ListAsync(
            $"{literal}-{marker}",
            FoodActivityFilter.All,
            new PageRequest(1, 100),
            token
        );

        Assert.Single(result.Items);
        Assert.Equal(expected.Id, result.Items[0].Id);
    }

    [Fact]
    public async Task Update_Global_ReturnsReadOnlyWithoutMutation()
    {
        var token = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(Guid.NewGuid().ToString("N"), token);
        var global = CreateFood(null, $"global-{Guid.NewGuid():N}");
        await using (var admin = _fixture.CreateAdministrativeContext(tenant.TrainerId))
        {
            admin.Foods.Add(global);
            admin.AdministrativeAuditEntries.Add(CreateAudit(tenant.TrainerId, global));
            await admin.SaveChangesAsync(token);
        }

        await using var context = _fixture.CreateContext(tenant.TrainerId);
        var result = await new FoodStore(context).UpdateAsync(
            global.Id,
            tenant.TrainerId,
            "Changed",
            null,
            1m,
            1m,
            1m,
            null,
            Now.AddHours(1),
            token
        );

        Assert.Equal(FoodStoreResult.Status.GlobalReadOnly, result.Kind);
        await using var verify = _fixture.CreateAdministrativeContext();
        Assert.Equal(
            global.Name,
            (await verify.Foods.IgnoreQueryFilters()
                .SingleAsync(food => food.Id == global.Id, token)).Name
        );
    }

    [Fact]
    public async Task Archive_TwoWorkers_ChangesExactlyOnce()
    {
        var token = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(Guid.NewGuid().ToString("N"), token);
        var food = CreateFood(tenant.TrainerId, $"food-{Guid.NewGuid():N}");
        await using (var seed = _fixture.CreateContext(tenant.TrainerId))
        {
            seed.Foods.Add(food);
            await seed.SaveChangesAsync(token);
        }

        await using var contextA = _fixture.CreateContext(tenant.TrainerId);
        await using var contextB = _fixture.CreateContext(tenant.TrainerId);
        var results = await Task.WhenAll(
            new FoodStore(contextA).SetActiveAsync(
                food.Id,
                tenant.TrainerId,
                false,
                Now.AddHours(1),
                token
            ),
            new FoodStore(contextB).SetActiveAsync(
                food.Id,
                tenant.TrainerId,
                false,
                Now.AddHours(1),
                token
            )
        );

        Assert.Single(results, result => result.Kind == FoodStoreResult.Status.Changed);
        Assert.Single(
            results,
            result => result.Kind == FoodStoreResult.Status.AlreadyInRequestedState
        );
        await using var verify = _fixture.CreateContext(tenant.TrainerId);
        Assert.False((await verify.Foods.SingleAsync(value => value.Id == food.Id, token)).IsActive);
    }

    private static AdministrativeAuditEntry CreateAudit(Guid actorUserId, Food food) =>
        new(actorUserId, "create", "food", food.Id, null, "{}", Now);

    private static Food CreateFood(Guid? trainerId, string name) =>
        new(trainerId, name, null, 2m, 20m, 1m, null, Now);
}
