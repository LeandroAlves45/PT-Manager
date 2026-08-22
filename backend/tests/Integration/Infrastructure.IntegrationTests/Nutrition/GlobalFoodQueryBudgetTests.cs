using System.Data.Common;
using Application.Features.Nutrition.Foods.ListGlobalFoods;
using Application.Pagination;
using Infrastructure.Data;
using Infrastructure.Data.Interceptors;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Errors;
using Infrastructure.Persistence.Nutrition;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.IntegrationTests.Nutrition;

[Collection(PostgresCollection.Name)]
public sealed class GlobalFoodQueryBudgetTests
{
    private readonly PostgresContainerFixture _fixture;

    public GlobalFoodQueryBudgetTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Get_UsesExactlyOneCommand()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var counter = new CommandCounter();
        await using var context = CreateMeasuredContext(tenant.TrainerId, counter);
        var queries = new GlobalFoodQueries(context);

        await queries.GetAsync(Guid.NewGuid(), cancellationToken);

        Assert.Equal(1, counter.ReaderCommands);
    }

    [Fact]
    public async Task List_UsesCountAndPagedQuery()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var counter = new CommandCounter();
        await using var context = CreateMeasuredContext(tenant.TrainerId, counter);
        var queries = new GlobalFoodQueries(context);

        await queries.ListAsync(
            null, GlobalFoodActivityFilter.Active, new PageRequest(1, 20), cancellationToken);

        Assert.Equal(2, counter.ReaderCommands);
    }

    [Fact]
    public async Task Create_UsesOneAtomicWriteCommand()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var counter = new CommandCounter();
        await using var context = CreateMeasuredContext(tenant.TrainerId, counter);
        var store = new GlobalFoodStore(context, new PostgresConstraintTranslator());

        await store.CreateAsync(
            tenant.TrainerId,
            $"food-{Guid.NewGuid():N}",
            null,
            2m,
            20m,
            1m,
            null,
            DateTime.UtcNow,
            cancellationToken);

        Assert.Equal(1, counter.ReaderCommands);
    }

    [Fact]
    public async Task Update_UsesLockReferenceCheckAndAtomicWrite()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var foodId = await SeedGlobalFoodAsync(tenant.TrainerId, cancellationToken);
        var counter = new CommandCounter();
        await using var context = CreateMeasuredContext(tenant.TrainerId, counter);
        var store = new GlobalFoodStore(context, new PostgresConstraintTranslator());

        await store.UpdateAsync(
            tenant.TrainerId,
            foodId,
            "Updated food",
            null,
            3m,
            21m,
            2m,
            null,
            DateTime.UtcNow,
            cancellationToken);

        Assert.Equal(3, counter.ReaderCommands);
    }

    [Fact]
    public async Task Archive_UsesLockAndAtomicWrite()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var foodId = await SeedGlobalFoodAsync(tenant.TrainerId, cancellationToken);
        var counter = new CommandCounter();
        await using var context = CreateMeasuredContext(tenant.TrainerId, counter);
        var store = new GlobalFoodStore(context, new PostgresConstraintTranslator());

        await store.SetActiveAsync(
            tenant.TrainerId,
            foodId,
            false,
            DateTime.UtcNow,
            cancellationToken);

        Assert.Equal(2, counter.ReaderCommands);
    }

    [Fact]
    public async Task Delete_UsesLockReferenceCheckAndAtomicWrite()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var foodId = await SeedGlobalFoodAsync(tenant.TrainerId, cancellationToken);
        var counter = new CommandCounter();
        await using var context = CreateMeasuredContext(tenant.TrainerId, counter);
        var store = new GlobalFoodStore(context, new PostgresConstraintTranslator());

        await store.DeleteAsync(
            tenant.TrainerId,
            foodId,
            DateTime.UtcNow,
            cancellationToken);

        Assert.Equal(3, counter.ReaderCommands);
    }

    private async Task<Guid> SeedGlobalFoodAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var context = _fixture.CreateAdministrativeContext(actorUserId);
        var store = new GlobalFoodStore(context, new PostgresConstraintTranslator());
        var result = await store.CreateAsync(
            actorUserId,
            $"food-{Guid.NewGuid():N}",
            null,
            2m,
            20m,
            1m,
            null,
            DateTime.UtcNow,
            cancellationToken);
        return result.Food!.Id;
    }

    private PtManagerDbContext CreateMeasuredContext(Guid trainerId, CommandCounter counter)
    {
        var tenantContext = TestTenantContext.Administrator(trainerId);
        var options = new DbContextOptionsBuilder<PtManagerDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .AddInterceptors(new TenantWriteValidationInterceptor(tenantContext), counter)
            .Options;
        return new PtManagerDbContext(options, tenantContext);
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int ReaderCommands { get; private set; }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            ReaderCommands++;
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ReaderCommands++;
            return ValueTask.FromResult(result);
        }
    }
}
