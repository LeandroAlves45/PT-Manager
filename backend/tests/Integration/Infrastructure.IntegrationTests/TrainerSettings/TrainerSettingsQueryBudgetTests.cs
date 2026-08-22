using System.Data.Common;
using Infrastructure.Data;
using Infrastructure.Data.Interceptors;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Clients;
using Infrastructure.Persistence.TrainerSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.IntegrationTests.TrainerSettings;

[Collection(PostgresCollection.Name)]
public sealed class TrainerSettingsQueryBudgetTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);
    private readonly PostgresContainerFixture _fixture;

    public TrainerSettingsQueryBudgetTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetTrainerSettings_UsesExactlyOneCommand()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var counter = new CommandCounter();
        await using var context = CreateMeasuredContext(tenant.TrainerId, counter);
        var queries = new Infrastructure.Persistence.TrainerSettings.TrainerSettingsQueries(context);

        var result = await queries.GetAsync(tenant.TrainerId, cancellationToken);

        Assert.NotNull(result);
        Assert.Equal(1, counter.ReaderCommands);
    }

    [Fact]
    public async Task GetClientBranding_UsesExactlyOneCommand()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var counter = new CommandCounter();
        await using var context = CreateMeasuredContext(tenant.TrainerId, counter);
        var queries = new ClientBrandingQueries(context);

        var result = await queries.GetAsync(
            tenant.TrainerId, tenant.ClientUserId, cancellationToken);

        Assert.NotNull(result);
        Assert.Equal(1, counter.ReaderCommands);
    }

    [Fact]
    public async Task UpdateBranding_UsesReadAndWrite()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var counter = new CommandCounter();
        await using var context = CreateMeasuredContext(tenant.TrainerId, counter);
        var store = new TrainerSettingsStore(context);

        await store.UpdateBrandingAsync(
            tenant.TrainerId,
            "Studio Fit",
            "#112233",
            "#FFFFFF",
            Now,
            cancellationToken);

        Assert.Equal(2, counter.ReaderCommands);
    }

    [Fact]
    public async Task ChangeTimezone_UsesLocksConflictCheckAndWrite()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var counter = new CommandCounter();
        await using var context = CreateMeasuredContext(tenant.TrainerId, counter);
        var store = new TrainerSettingsStore(context);

        await store.ChangeTimezoneAsync(
            tenant.TrainerId,
            "Europe/Madrid",
            Now,
            cancellationToken);

        Assert.Equal(4, counter.ReaderCommands);
    }

    [Fact]
    public async Task ReplaceLogo_UsesLocksAndAtomicSettingsOutboxWrite()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        await using (var seed = _fixture.CreateContext(tenant.TrainerId))
        {
            await new TrainerSettingsStore(seed).ReplaceLogoAsync(
                tenant.TrainerId,
                "https://cdn/old.png",
                "old-logo",
                Guid.NewGuid(),
                Now,
                cancellationToken);
        }
        var counter = new CommandCounter();
        await using var context = CreateMeasuredContext(tenant.TrainerId, counter);
        var store = new TrainerSettingsStore(context);

        await store.ReplaceLogoAsync(
            tenant.TrainerId,
            "https://cdn/new.png",
            "new-logo",
            Guid.NewGuid(),
            Now.AddMinutes(1),
            cancellationToken);

        Assert.Equal(3, counter.ReaderCommands);
    }

    private PtManagerDbContext CreateMeasuredContext(Guid trainerId, CommandCounter counter)
    {
        var tenantContext = TestTenantContext.ForTrainer(trainerId);
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
