using System.Text.Json;
using Application.Features.Nutrition.Foods.Abstractions;
using Domain.Entities.Administration;
using Infrastructure.Data;
using Infrastructure.Data.Interceptors;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Errors;
using Infrastructure.Persistence.Nutrition;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Nutrition;

[Collection(PostgresCollection.Name)]
public sealed class GlobalFoodPersistenceTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);
    private readonly PostgresContainerFixture _fixture;

    public GlobalFoodPersistenceTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Create_WritesFoodAndMatchingKcalAuditInSameTransaction()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        await using var context = CreateAdminContext(tenant.TrainerId);
        var store = new GlobalFoodStore(context, new PostgresConstraintTranslator());

        var outcome = await store.CreateAsync(
            tenant.TrainerId, "Rice", null, 2.7m, 28m, 0.3m, 0.4m, Now, cancellationToken);

        Assert.Equal(GlobalFoodStoreResult.Status.Created, outcome.Kind);
        Assert.Equal(125.5m, outcome.Food!.Kcal);
        var audit = await context.AdministrativeAuditEntries.SingleAsync(
            entry => entry.ResourceId == outcome.Food.Id && entry.Action == "create",
            cancellationToken);
        using var afterState = JsonDocument.Parse(audit.AfterState!);
        Assert.Equal(125.5m, afterState.RootElement.GetProperty("kcal").GetDecimal());
    }

    [Fact]
    public async Task Update_WhenFoodIsReferencedByMealPlan_ReturnsReferencedAndSkipsAudit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        Guid foodId;
        await using (var admin = CreateAdminContext(tenant.TrainerId))
        {
            var globalStore = new GlobalFoodStore(admin, new PostgresConstraintTranslator());
            var created = await globalStore.CreateAsync(
                tenant.TrainerId, "Rice", null, 2.7m, 28m, 0.3m, 0.4m, Now, cancellationToken);
            foodId = created.Food!.Id;
        }
        await using (var trainer = _fixture.CreateContext(tenant.TrainerId))
        {
            await IntegrationTestData.SeedMealPlanReferencingFoodAsync(
                trainer, tenant.TrainerId, tenant.ClientId, foodId, Now, cancellationToken);
        }
        await using var context = CreateAdminContext(tenant.TrainerId);
        var store = new GlobalFoodStore(context, new PostgresConstraintTranslator());

        var outcome = await store.UpdateAsync(
            tenant.TrainerId, foodId, "Basmati rice", null, 2.7m, 28m, 0.3m, 0.4m,
            Now.AddMinutes(1), cancellationToken);

        Assert.Equal(GlobalFoodStoreResult.Status.Referenced, outcome.Kind);
        Assert.False(await context.AdministrativeAuditEntries.AnyAsync(
            entry => entry.ResourceId == foodId && entry.Action == "update", cancellationToken));
    }

    [Fact]
    public async Task Delete_WhenFoodHasReference_ReturnsHasReferencesAndPreservesRow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        Guid foodId;
        await using (var admin = CreateAdminContext(tenant.TrainerId))
        {
            var globalStore = new GlobalFoodStore(admin, new PostgresConstraintTranslator());
            var created = await globalStore.CreateAsync(
                tenant.TrainerId, "Rice", null, 2.7m, 28m, 0.3m, 0.4m, Now, cancellationToken);
            foodId = created.Food!.Id;
        }
        await using (var trainer = _fixture.CreateContext(tenant.TrainerId))
        {
            await IntegrationTestData.SeedMealPlanReferencingFoodAsync(
                trainer, tenant.TrainerId, tenant.ClientId, foodId, Now, cancellationToken);
        }
        await using var context = CreateAdminContext(tenant.TrainerId);
        var store = new GlobalFoodStore(context, new PostgresConstraintTranslator());

        var outcome = await store.DeleteAsync(
            tenant.TrainerId, foodId, Now.AddMinutes(1), cancellationToken);

        Assert.Equal(GlobalFoodStoreResult.Status.HasReferences, outcome.Kind);
        Assert.True(await context.Foods.IgnoreQueryFilters()
            .AnyAsync(food => food.Id == foodId, cancellationToken));
    }

    [Fact]
    public async Task Delete_WhenUnreferenced_RemovesFoodButPreservesAuditHistory()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        await using var context = CreateAdminContext(tenant.TrainerId);
        var store = new GlobalFoodStore(context, new PostgresConstraintTranslator());
        var created = await store.CreateAsync(
            tenant.TrainerId, "Rice", null, 2.7m, 28m, 0.3m, 0.4m, Now, cancellationToken);

        var outcome = await store.DeleteAsync(
            tenant.TrainerId, created.Food!.Id, Now.AddMinutes(1), cancellationToken);

        Assert.Equal(GlobalFoodStoreResult.Status.Deleted, outcome.Kind);
        Assert.False(await context.Foods.IgnoreQueryFilters()
            .AnyAsync(food => food.Id == created.Food.Id, cancellationToken));
        Assert.Equal(2, await context.AdministrativeAuditEntries.CountAsync(
            entry => entry.ResourceId == created.Food.Id, cancellationToken));
    }

    [Fact]
    public async Task Archive_IsIdempotentAndSkipsSecondAuditEntry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        await using var context = CreateAdminContext(tenant.TrainerId);
        var store = new GlobalFoodStore(context, new PostgresConstraintTranslator());
        var created = await store.CreateAsync(
            tenant.TrainerId, "Rice", null, 2.7m, 28m, 0.3m, 0.4m, Now, cancellationToken);
        await store.SetActiveAsync(
            tenant.TrainerId, created.Food!.Id, false, Now.AddMinutes(1), cancellationToken);

        var outcome = await store.SetActiveAsync(
            tenant.TrainerId, created.Food.Id, false, Now.AddMinutes(2), cancellationToken);

        Assert.Equal(GlobalFoodStoreResult.Status.AlreadyInRequestedState, outcome.Kind);
        Assert.Equal(2, await context.AdministrativeAuditEntries.CountAsync(
            entry => entry.ResourceId == created.Food.Id, cancellationToken));
    }

    private PtManagerDbContext CreateAdminContext(Guid actorUserId)
    {
        var tenantContext = TestTenantContext.Administrator(actorUserId);
        var options = new DbContextOptionsBuilder<PtManagerDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .AddInterceptors(new TenantWriteValidationInterceptor(tenantContext))
            .Options;
        return new PtManagerDbContext(options, tenantContext);
    }
}
