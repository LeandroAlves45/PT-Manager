using Infrastructure.Data;
using Infrastructure.Data.Interceptors;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Common;
using Infrastructure.Persistence.Errors;
using Infrastructure.Persistence.Nutrition;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.IntegrationTests.Common;

[Collection(PostgresCollection.Name)]
public sealed class CatalogReferenceLockingTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);
    private readonly PostgresContainerFixture _fixture;

    public CatalogReferenceLockingTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ForShareLock_BlocksConcurrentAdminArchiveUntilTransactionEnds()
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

        await using var lockingContext = _fixture.CreateContext(tenant.TrainerId);
        await using var lockingTransaction = await lockingContext.Database
            .BeginTransactionAsync(cancellationToken);
        await lockingContext.LockFoodsForShareAsync(
            tenant.TrainerId,
            [foodId],
            cancellationToken);

        await using var adminContext = CreateAdminContext(tenant.TrainerId);
        await adminContext.Database.OpenConnectionAsync(cancellationToken);
        await using var backendPidCommand = adminContext.Database.GetDbConnection().CreateCommand();
        backendPidCommand.CommandText = "SELECT pg_backend_pid();";
        var backendPid = (int)(await backendPidCommand.ExecuteScalarAsync(cancellationToken))!;
        var adminStore = new GlobalFoodStore(adminContext, new PostgresConstraintTranslator());
        var archiveTask = adminStore.SetActiveAsync(
            tenant.TrainerId, foodId, false, Now.AddMinutes(1), CancellationToken.None);

        // A observação do backend PostgreSQL prova que a operação concorrente
        // chegou ao servidor e está realmente à espera do lock FOR SHARE.
        Assert.True(await WaitUntilBackendWaitsForLockAsync(backendPid, cancellationToken));
        Assert.False(archiveTask.IsCompleted);

        await lockingTransaction.CommitAsync(cancellationToken);
        var outcome = await archiveTask;
        Assert.Equal(Application.Features.Nutrition.Foods.Abstractions
            .GlobalFoodStoreResult.Status.Changed, outcome.Kind);
    }

    [Fact]
    public async Task LockFoodsForShareAsync_WithEmptyIds_ReturnsEmptyWithoutQuerying()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        await using var context = _fixture.CreateContext(tenant.TrainerId);

        var locked = await context.LockFoodsForShareAsync(
            tenant.TrainerId,
            [],
            cancellationToken);

        Assert.Empty(locked);
    }

    [Fact]
    public async Task LockFoodsForShareAsync_WithReversedInput_ReturnsStableIdOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var first = IntegrationTestData.Food(null, Now);
        var second = new Domain.Entities.Nutrition.Food(
            null,
            "Oats",
            null,
            13m,
            68m,
            7m,
            10m,
            Now);
        await using (var admin = CreateAdminContext(tenant.TrainerId))
        {
            admin.Foods.AddRange(first, second);
            admin.AdministrativeAuditEntries.AddRange(
                new Domain.Entities.Administration.AdministrativeAuditEntry(
                    tenant.TrainerId,
                    "create",
                    "food",
                    first.Id,
                    null,
                    "{}",
                    Now),
                new Domain.Entities.Administration.AdministrativeAuditEntry(
                    tenant.TrainerId,
                    "create",
                    "food",
                    second.Id,
                    null,
                    "{}",
                    Now));
            await admin.SaveChangesAsync(cancellationToken);
        }
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken);

        var locked = await context.LockFoodsForShareAsync(
            tenant.TrainerId,
            [second.Id, first.Id],
            cancellationToken);

        Assert.Equal(
            new[] { first.Id, second.Id }.OrderBy(id => id),
            locked.Select(food => food.Id));
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

    private async Task<bool> WaitUntilBackendWaitsForLockAsync(
        int backendPid,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM pg_stat_activity
                WHERE pid = @backend_pid
                    AND wait_event_type = 'Lock');
            """;

        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (await _fixture.QueryScalarAsync<bool>(
                sql,
                cancellationToken,
                new NpgsqlParameter("backend_pid", backendPid)))
                return true;

            await Task.Delay(100, cancellationToken);
        }

        return false;
    }
}
