using Application.Features.Clients.ListClients;
using Application.Pagination;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Clients;

namespace Infrastructure.IntegrationTests.Clients;

/// <summary>Verifica projeções, filtros e ordenação em PostgreSQL real.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ClientQueriesTests
{
    private readonly PostgresContainerFixture _fixture;

    public ClientQueriesTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task List_ReturnsOnlyCurrentTenant()
    {
        var discriminator = NewDiscriminator();
        var trainerA = ClientPersistenceTestData.CreateTrainer($"a-{discriminator}");
        var trainerB = ClientPersistenceTestData.CreateTrainer($"b-{discriminator}");
        var clientA = ClientPersistenceTestData.CreateClient(trainerA.Id, $"a-{discriminator}");
        var clientB = ClientPersistenceTestData.CreateClient(trainerB.Id, $"b-{discriminator}");
        await ClientPersistenceTestData.PersistAsync(_fixture, trainerA.Id, trainerA, clientA);
        await ClientPersistenceTestData.PersistAsync(_fixture, trainerB.Id, trainerB, clientB);

        await using var context = _fixture.CreateContext(trainerA.Id);
        var queries = new ClientQueries(context);

        var result = await queries.ListAsync(
            search: null,
            ClientActivityFilter.All,
            new PageRequest(1, 50),
            CancellationToken.None);

        Assert.Contains(result.Items, item => item.Id == clientA.Id);
        Assert.DoesNotContain(result.Items, item => item.Id == clientB.Id);
    }

    [Fact]
    public async Task List_ActiveFilter_ReturnsOnlyActiveClients()
    {
        var seed = await SeedActivityClientsAsync();
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var queries = new ClientQueries(context);

        var result = await queries.ListAsync(
            null,
            ClientActivityFilter.Active,
            new PageRequest(1, 50),
            CancellationToken.None);

        Assert.Contains(result.Items, item => item.Id == seed.ActiveClientId);
        Assert.DoesNotContain(result.Items, item => item.Id == seed.ArchivedClientId);
    }

    [Fact]
    public async Task List_ArchivedFilter_ReturnsOnlyArchivedClients()
    {
        var seed = await SeedActivityClientsAsync();
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var queries = new ClientQueries(context);

        var result = await queries.ListAsync(
            null,
            ClientActivityFilter.Archived,
            new PageRequest(1, 50),
            CancellationToken.None);

        Assert.Contains(result.Items, item => item.Id == seed.ArchivedClientId);
        Assert.DoesNotContain(result.Items, item => item.Id == seed.ActiveClientId);
    }

    [Fact]
    public async Task List_AllFilter_ReturnsActiveAndArchivedClients()
    {
        var seed = await SeedActivityClientsAsync();
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var queries = new ClientQueries(context);

        var result = await queries.ListAsync(
            null,
            ClientActivityFilter.All,
            new PageRequest(1, 50),
            CancellationToken.None);

        Assert.Contains(result.Items, item => item.Id == seed.ActiveClientId);
        Assert.Contains(result.Items, item => item.Id == seed.ArchivedClientId);
    }

    [Fact]
    public async Task List_UsesStableNameThenIdOrderAcrossPages()
    {
        var discriminator = NewDiscriminator();
        var trainer = ClientPersistenceTestData.CreateTrainer(discriminator);
        var clients = new[]
        {
            ClientPersistenceTestData.CreateClient(trainer.Id, $"1-{discriminator}", name: "Same Name"),
            ClientPersistenceTestData.CreateClient(trainer.Id, $"2-{discriminator}", name: "Same Name"),
            ClientPersistenceTestData.CreateClient(trainer.Id, $"3-{discriminator}", name: "Same Name")
        };
        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            trainer.Id,
            new object[] { trainer }.Concat(clients.Cast<object>()).ToArray());
        await using var context = _fixture.CreateContext(trainer.Id);
        var queries = new ClientQueries(context);

        var firstPage = await queries.ListAsync(
            null,
            ClientActivityFilter.All,
            new PageRequest(1, 2),
            CancellationToken.None);
        var secondPage = await queries.ListAsync(
            null,
            ClientActivityFilter.All,
            new PageRequest(2, 2),
            CancellationToken.None);
        var repeatedFirstPage = await queries.ListAsync(
            null,
            ClientActivityFilter.All,
            new PageRequest(1, 2),
            CancellationToken.None);
        var combinedIds = firstPage.Items.Concat(secondPage.Items).Select(item => item.Id).ToArray();

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(3, combinedIds.Length);
        Assert.Equal(3, combinedIds.Distinct().Count());
        Assert.Equal(
            firstPage.Items.Select(item => item.Id),
            repeatedFirstPage.Items.Select(item => item.Id));
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("\\")]
    public async Task List_SearchTreatsLikeWildcardAsLiteral(string search)
    {
        var discriminator = NewDiscriminator();
        var trainer = ClientPersistenceTestData.CreateTrainer(discriminator);
        var literalClient = ClientPersistenceTestData.CreateClient(
            trainer.Id,
            $"literal-{discriminator}",
            name: $"Literal {search} value");
        var controlClient = ClientPersistenceTestData.CreateClient(
            trainer.Id,
            $"control-{discriminator}",
            name: "Control value");
        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            trainer.Id,
            trainer,
            literalClient,
            controlClient);
        await using var context = _fixture.CreateContext(trainer.Id);
        var queries = new ClientQueries(context);

        var result = await queries.ListAsync(
            search,
            ClientActivityFilter.All,
            new PageRequest(1, 50),
            CancellationToken.None);

        Assert.Contains(result.Items, item => item.Id == literalClient.Id);
        Assert.DoesNotContain(result.Items, item => item.Id == controlClient.Id);
    }

    [Fact]
    public async Task Details_ReturnsEveryUsablePackInCanonicalOrder()
    {
        var discriminator = NewDiscriminator();
        var today = DateOnly.FromDateTime(ClientPersistenceTestData.NowUtc);
        var trainer = ClientPersistenceTestData.CreateTrainer(discriminator);
        var client = ClientPersistenceTestData.CreateClient(trainer.Id, discriminator);
        var packType = ClientPersistenceTestData.CreatePackType(trainer.Id, discriminator, 2);
        var expired = ClientPersistenceTestData.CreatePack(
            trainer.Id,
            client.Id,
            packType,
            today.AddDays(-30),
            today.AddDays(-1));
        var empty = ClientPersistenceTestData.CreatePack(
            trainer.Id,
            client.Id,
            packType,
            today.AddDays(-10),
            today.AddDays(10),
            sessionsToConsume: 2);
        var expiresToday = ClientPersistenceTestData.CreatePack(
            trainer.Id,
            client.Id,
            packType,
            today.AddDays(-10),
            today);
        var futureEarly = ClientPersistenceTestData.CreatePack(
            trainer.Id,
            client.Id,
            packType,
            today.AddDays(-10),
            today.AddDays(10),
            now: ClientPersistenceTestData.NowUtc.AddMinutes(1));
        var futureLate = ClientPersistenceTestData.CreatePack(
            trainer.Id,
            client.Id,
            packType,
            today.AddDays(-10),
            today.AddDays(10),
            now: ClientPersistenceTestData.NowUtc.AddMinutes(2));
        var withoutExpiration = ClientPersistenceTestData.CreatePack(
            trainer.Id,
            client.Id,
            packType,
            today.AddDays(-10),
            expirationDate: null,
            now: ClientPersistenceTestData.NowUtc.AddMinutes(3));
        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            trainer.Id,
            trainer,
            client,
            packType,
            expired,
            empty,
            expiresToday,
            futureEarly,
            futureLate,
            withoutExpiration);
        await using var context = _fixture.CreateContext(trainer.Id);
        var queries = new ClientQueries(context);

        var result = await queries.GetDetailsAsync(client.Id, today, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(client.Name, result.Name);
        Assert.Equal(
            new[] { expiresToday.Id, futureEarly.Id, futureLate.Id, withoutExpiration.Id },
            result.UsablePacks.Select(pack => pack.Id));
        Assert.DoesNotContain(result.UsablePacks, pack => pack.Id == expired.Id);
        Assert.DoesNotContain(result.UsablePacks, pack => pack.Id == empty.Id);
    }

    [Fact]
    public async Task Details_OtherTenant_ReturnsNull()
    {
        var discriminator = NewDiscriminator();
        var trainerA = ClientPersistenceTestData.CreateTrainer($"a-{discriminator}");
        var trainerB = ClientPersistenceTestData.CreateTrainer($"b-{discriminator}");
        var clientB = ClientPersistenceTestData.CreateClient(trainerB.Id, discriminator);
        await ClientPersistenceTestData.PersistAsync(_fixture, trainerA.Id, trainerA);
        await ClientPersistenceTestData.PersistAsync(_fixture, trainerB.Id, trainerB, clientB);
        await using var context = _fixture.CreateContext(trainerA.Id);
        var queries = new ClientQueries(context);

        var result = await queries.GetDetailsAsync(
            clientB.Id,
            DateOnly.FromDateTime(ClientPersistenceTestData.NowUtc),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task UsablePacks_OtherTenant_ReturnsEmpty()
    {
        var discriminator = NewDiscriminator();
        var today = DateOnly.FromDateTime(ClientPersistenceTestData.NowUtc);
        var trainerA = ClientPersistenceTestData.CreateTrainer($"a-{discriminator}");
        var trainerB = ClientPersistenceTestData.CreateTrainer($"b-{discriminator}");
        var clientB = ClientPersistenceTestData.CreateClient(trainerB.Id, discriminator);
        var packTypeB = ClientPersistenceTestData.CreatePackType(trainerB.Id, discriminator);
        var packB = ClientPersistenceTestData.CreatePack(
            trainerB.Id,
            clientB.Id,
            packTypeB,
            today,
            today.AddDays(10));
        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            trainerA.Id,
            trainerA);
        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            trainerB.Id,
            trainerB,
            clientB,
            packTypeB,
            packB);
        await using var context = _fixture.CreateContext(trainerA.Id);
        var queries = new ClientQueries(context);

        var result = await queries.ListUsablePacksAsync(
            clientB.Id,
            today,
            CancellationToken.None);

        Assert.Empty(result);
    }

    private async Task<ActivitySeed> SeedActivityClientsAsync()
    {
        var discriminator = NewDiscriminator();
        var trainer = ClientPersistenceTestData.CreateTrainer(discriminator);
        var activeClient = ClientPersistenceTestData.CreateClient(
            trainer.Id,
            $"active-{discriminator}");
        var archivedClient = ClientPersistenceTestData.CreateClient(
            trainer.Id,
            $"archived-{discriminator}",
            isActive: false);
        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            trainer.Id,
            trainer,
            activeClient,
            archivedClient);
        return new ActivitySeed(trainer.Id, activeClient.Id, archivedClient.Id);
    }

    private static string NewDiscriminator() => Guid.NewGuid().ToString("N");

    private sealed record ActivitySeed(
        Guid TrainerId,
        Guid ActiveClientId,
        Guid ArchivedClientId);
}
