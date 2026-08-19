using System.Data.Common;
using Application.Features.Supplements.ListSupplementAssignments;
using Application.Features.Supplements.ListSupplements;
using Application.Pagination;
using Domain.Entities.Supplements;
using Infrastructure.Data;
using Infrastructure.Data.Interceptors;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Supplements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.IntegrationTests.Supplements;

[Collection(PostgresCollection.Name)]
public sealed class SupplementQueryBudgetTests
{
    private static readonly DateTime Now = new(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);
    private readonly PostgresContainerFixture _fixture;

    public SupplementQueryBudgetTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CatalogGet_UsesExactlyOneCommand()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var supplement = IntegrationTestData.Supplement(
            tenant.TrainerId, Now, tenant.TrainerId);
        await using (var seed = _fixture.CreateContext(tenant.TrainerId))
        {
            seed.Supplements.Add(supplement);
            await seed.SaveChangesAsync(cancellationToken);
        }
        var counter = new CommandCounter();
        await using var context = CreateMeasuredContext(tenant.TrainerId, counter);
        var queries = new SupplementQueries(context);

        var result = await queries.GetAsync(
            tenant.TrainerId, supplement.Id, cancellationToken);

        Assert.NotNull(result);
        Assert.Equal(1, counter.ReaderCommands);
    }

    [Fact]
    public async Task CatalogList_UsesCountAndPagedQuery()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var counter = new CommandCounter();
        await using var context = CreateMeasuredContext(tenant.TrainerId, counter);
        var queries = new SupplementQueries(context);

        await queries.ListAsync(
            tenant.TrainerId, null, SupplementActivityFilter.Active,
            new PageRequest(1, 20), cancellationToken);

        Assert.Equal(2, counter.ReaderCommands);
    }

    [Fact]
    public async Task ClientDetail_WhenClientIsArchived_UsesOneJoinAndPreservesHistory()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var supplement = new Supplement(
            tenant.TrainerId, tenant.TrainerId, "Creatine", null,
            "grams", "5 g", "daily", "internal-only", Now);
        var assignment = new ClientSupplementAssignment(
            tenant.TrainerId, tenant.ClientId, supplement.Id,
            "3 g", "evening", "client-visible", Now);
        await using (var seed = _fixture.CreateContext(tenant.TrainerId))
        {
            seed.Supplements.Add(supplement);
            seed.ClientSupplementAssignments.Add(assignment);
            var client = await seed.Clients.SingleAsync(
                item => item.Id == tenant.ClientId, cancellationToken);
            client.Deactivate(Now.AddMinutes(1));
            await seed.SaveChangesAsync(cancellationToken);
        }
        var counter = new CommandCounter();
        await using var context = CreateMeasuredContext(tenant.TrainerId, counter);
        var queries = new ClientSupplementAssignmentQueries(context);

        var result = await queries.GetMyAsync(
            tenant.TrainerId, tenant.ClientUserId, assignment.Id, cancellationToken);

        Assert.NotNull(result);
        Assert.Equal("client-visible", result.TrainerNotes);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(
            counter.LastCommandText, "trainer_notes",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Cast<
                System.Text.RegularExpressions.Match>());
        Assert.Equal(1, counter.ReaderCommands);
    }

    [Fact]
    public async Task ClientList_UsesCountAndPagedJoinedQuery()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(
            Guid.NewGuid().ToString("N"), cancellationToken);
        var counter = new CommandCounter();
        await using var context = CreateMeasuredContext(tenant.TrainerId, counter);
        var queries = new ClientSupplementAssignmentQueries(context);

        await queries.ListMyActiveAsync(
            tenant.TrainerId, tenant.ClientUserId,
            new PageRequest(1, 20), cancellationToken);

        Assert.Equal(2, counter.ReaderCommands);
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
        public string LastCommandText { get; private set; } = string.Empty;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Record(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Record(command);
            return ValueTask.FromResult(result);
        }

        private void Record(DbCommand command)
        {
            ReaderCommands++;
            LastCommandText = command.CommandText;
        }
    }
}
