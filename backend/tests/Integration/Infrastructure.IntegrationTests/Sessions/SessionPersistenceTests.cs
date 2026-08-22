using Application.Features.Sessions.Abstractions;
using TrainerSettingsEntity = global::Domain.Entities.TrainerSettings.TrainerSettings;
using Domain.ValueObjects;
using Infrastructure.IntegrationTests.Clients;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Errors;
using Infrastructure.Persistence.Sessions;
using Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Sessions;

[Collection(PostgresCollection.Name)]
public sealed class SessionPersistenceTests
{
    private static readonly DateTime Now =
        new(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc);

    private readonly PostgresContainerFixture _fixture;

    public SessionPersistenceTests(PostgresContainerFixture fixture) =>
        _fixture = fixture;

    [Fact]
    public async Task Create_ArchivedClient_ReturnsClientInactive()
    {
        var seed = await SeedAsync(clientActive: false);
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var result = await CreateStore(context).CreateAsync(
            seed.TrainerId,
            seed.ClientId,
            null,
            Start(1),
            60,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);

        Assert.Equal(SessionStoreResult.Status.ClientInactive, result.Kind);
    }

    [Fact]
    public async Task Create_StartAtCurrentInstant_ReturnsValidationOutcome()
    {
        var seed = await SeedAsync();
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var result = await CreateStore(context).CreateAsync(
            seed.TrainerId,
            seed.ClientId,
            null,
            new DateTimeOffset(Now),
            60,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);

        Assert.Equal(SessionStoreResult.Status.StartsAtNotFuture, result.Kind);
    }

    [Fact]
    public async Task Create_ClientFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await SeedAsync();
        var other = await SeedAsync();
        await using var context = _fixture.CreateContext(owner.TrainerId);

        var result = await CreateStore(context).CreateAsync(
            owner.TrainerId,
            other.ClientId,
            null,
            Start(1),
            60,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);

        Assert.Equal(SessionStoreResult.Status.ClientNotFound, result.Kind);
    }

    [Fact]
    public async Task Create_SameClientSameLocalDay_ReturnsConflict()
    {
        var seed = await SeedAsync();
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = CreateStore(context);
        await store.CreateAsync(
            seed.TrainerId,
            seed.ClientId,
            null,
            Start(1),
            60,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);

        var result = await store.CreateAsync(
            seed.TrainerId,
            seed.ClientId,
            null,
            Start(1).AddHours(3),
            60,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);

        Assert.Equal(SessionStoreResult.Status.ClientDayConflict, result.Kind);
    }

    [Fact]
    public async Task Create_DifferentClientOverlapping_ReturnsTrainerConflict()
    {
        var seed = await SeedAsync(includeSecondClient: true);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = CreateStore(context);
        await store.CreateAsync(
            seed.TrainerId,
            seed.ClientId,
            null,
            Start(1),
            60,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);

        var result = await store.CreateAsync(
            seed.TrainerId,
            seed.SecondClientId!.Value,
            null,
            Start(1).AddMinutes(30),
            60,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);

        Assert.Equal(SessionStoreResult.Status.TrainerScheduleConflict, result.Kind);
    }

    [Fact]
    public async Task Create_AdjacentSessions_Succeeds()
    {
        var seed = await SeedAsync(includeSecondClient: true);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = CreateStore(context);
        await store.CreateAsync(
            seed.TrainerId,
            seed.ClientId,
            null,
            Start(1),
            60,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);

        var result = await store.CreateAsync(
            seed.TrainerId,
            seed.SecondClientId!.Value,
            null,
            Start(1).AddMinutes(60),
            60,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);

        Assert.Equal(SessionStoreResult.Status.Created, result.Kind);
    }

    [Fact]
    public async Task Complete_CompetingForLastPackUnit_OnlyOneSucceeds()
    {
        var seed = await SeedAsync(packSessions: 1);
        Guid firstId;
        Guid secondId;

        await using (var context = _fixture.CreateContext(seed.TrainerId))
        {
            var store = CreateStore(context);
            firstId = (await store.CreateAsync(
                seed.TrainerId,
                seed.ClientId,
                seed.PackId,
                Start(1),
                60,
                null,
                null,
                null,
                Now,
                TestContext.Current.CancellationToken)).Session!.Id;
            secondId = (await store.CreateAsync(
                seed.TrainerId,
                seed.ClientId,
                seed.PackId,
                Start(2),
                60,
                null,
                null,
                null,
                Now,
                TestContext.Current.CancellationToken)).Session!.Id;
        }

        var results = await Task.WhenAll(
            CompleteAsync(seed.TrainerId, firstId, Now.AddDays(3)),
            CompleteAsync(seed.TrainerId, secondId, Now.AddDays(3)));

        Assert.Single(
            results,
            result => result.Kind == SessionStoreResult.Status.Updated);
        Assert.Single(
            results,
            result => result.Kind == SessionStoreResult.Status.PackBalanceUnavailable);
    }

    [Fact]
    public async Task Complete_Repeated_DebitsPackOnce()
    {
        var seed = await SeedAsync(packSessions: 2);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = CreateStore(context);
        var created = await store.CreateAsync(
            seed.TrainerId,
            seed.ClientId,
            seed.PackId,
            Start(1),
            60,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);

        await store.TransitionAsync(
            seed.TrainerId,
            created.Session!.Id,
            SessionTransition.Complete,
            Now.AddDays(2),
            TestContext.Current.CancellationToken);
        var repeated = await store.TransitionAsync(
            seed.TrainerId,
            created.Session.Id,
            SessionTransition.Complete,
            Now.AddDays(3),
            TestContext.Current.CancellationToken);
        var balance = await context.ClientSessionPacks
            .Where(pack => pack.Id == seed.PackId)
            .Select(pack => pack.SessionsRemaining)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SessionStoreResult.Status.AlreadyInRequestedState, repeated.Kind);
        Assert.Equal(1, balance);
    }

    [Fact]
    public async Task Restore_PastCompleted_RestoresBalanceWithoutAgendaCheck()
    {
        var seed = await SeedAsync(packSessions: 2);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = CreateStore(context);
        var created = await store.CreateAsync(
            seed.TrainerId,
            seed.ClientId,
            seed.PackId,
            Start(1),
            60,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);
        await store.TransitionAsync(
            seed.TrainerId,
            created.Session!.Id,
            SessionTransition.Complete,
            Now.AddDays(2),
            TestContext.Current.CancellationToken);

        var restored = await store.TransitionAsync(
            seed.TrainerId,
            created.Session.Id,
            SessionTransition.Restore,
            Now.AddDays(3),
            TestContext.Current.CancellationToken);

        Assert.Equal(SessionStatus.Scheduled, restored.Session!.Status);
        Assert.Equal(2, await context.ClientSessionPacks
            .Where(pack => pack.Id == seed.PackId)
            .Select(pack => pack.SessionsRemaining)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MarkNoShow_ConsumesExactlyOnePackUnit()
    {
        var seed = await SeedAsync(packSessions: 2);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = CreateStore(context);
        var created = await store.CreateAsync(
            seed.TrainerId,
            seed.ClientId,
            seed.PackId,
            Start(1),
            60,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);

        var result = await store.TransitionAsync(
            seed.TrainerId,
            created.Session!.Id,
            SessionTransition.MarkNoShow,
            Now.AddDays(2),
            TestContext.Current.CancellationToken);

        Assert.Equal(SessionStatus.NoShow, result.Session!.Status);
        Assert.Equal(1, await context.ClientSessionPacks
            .Where(pack => pack.Id == seed.PackId)
            .Select(pack => pack.SessionsRemaining)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(SessionTransition.CancelByClient)]
    [InlineData(SessionTransition.CancelByTrainer)]
    public async Task Cancel_DoesNotChangePackBalance(SessionTransition transition)
    {
        var seed = await SeedAsync(packSessions: 2);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = CreateStore(context);
        var created = await store.CreateAsync(
            seed.TrainerId,
            seed.ClientId,
            seed.PackId,
            Start(1),
            60,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);

        await store.TransitionAsync(
            seed.TrainerId,
            created.Session!.Id,
            transition,
            Now.AddMinutes(1),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, await context.ClientSessionPacks
            .Where(pack => pack.Id == seed.PackId)
            .Select(pack => pack.SessionsRemaining)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ChangePack_RemoveAssociation_DoesNotChangeBalance()
    {
        var seed = await SeedAsync(packSessions: 2);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = CreateStore(context);
        var created = await store.CreateAsync(
            seed.TrainerId,
            seed.ClientId,
            seed.PackId,
            Start(1),
            60,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);

        var changed = await store.ChangePackAsync(
            seed.TrainerId,
            created.Session!.Id,
            null,
            Now.AddMinutes(1),
            TestContext.Current.CancellationToken);

        Assert.Null(changed.Session!.ClientSessionPackId);
        Assert.Equal(2, await context.ClientSessionPacks
            .Where(pack => pack.Id == seed.PackId)
            .Select(pack => pack.SessionsRemaining)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reschedule_IntoOccupiedInterval_RollsBackOriginalSchedule()
    {
        var seed = await SeedAsync(includeSecondClient: true);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = CreateStore(context);
        var first = await store.CreateAsync(
            seed.TrainerId,
            seed.ClientId,
            null,
            Start(1),
            60,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);
        await store.CreateAsync(
            seed.TrainerId,
            seed.SecondClientId!.Value,
            null,
            Start(2),
            60,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);

        var result = await store.RescheduleAsync(
            seed.TrainerId,
            first.Session!.Id,
            Start(2).AddMinutes(30),
            60,
            null,
            Now,
            TestContext.Current.CancellationToken);

        Assert.Equal(SessionStoreResult.Status.TrainerScheduleConflict, result.Kind);
        Assert.Equal(Start(1), await context.Sessions
            .Where(session => session.Id == first.Session.Id)
            .Select(session => session.StartsAt)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reschedule_StartAtCurrentInstant_ReturnsValidationOutcome()
    {
        var seed = await SeedAsync();
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = CreateStore(context);
        var created = await store.CreateAsync(
            seed.TrainerId,
            seed.ClientId,
            null,
            Start(1),
            60,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);

        var result = await store.RescheduleAsync(
            seed.TrainerId,
            created.Session!.Id,
            new DateTimeOffset(Now),
            60,
            null,
            Now,
            TestContext.Current.CancellationToken);

        Assert.Equal(SessionStoreResult.Status.StartsAtNotFuture, result.Kind);
    }

    [Fact]
    public async Task Restore_FutureCancelledSession_RevalidatesAgenda()
    {
        var seed = await SeedAsync(includeSecondClient: true);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = CreateStore(context);
        var cancelled = await store.CreateAsync(
            seed.TrainerId,
            seed.ClientId,
            null,
            Start(2),
            60,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);
        await store.TransitionAsync(
            seed.TrainerId,
            cancelled.Session!.Id,
            SessionTransition.CancelByClient,
            Now.AddMinutes(1),
            TestContext.Current.CancellationToken);
        await store.CreateAsync(
            seed.TrainerId,
            seed.SecondClientId!.Value,
            null,
            Start(2),
            60,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);

        var result = await store.TransitionAsync(
            seed.TrainerId,
            cancelled.Session.Id,
            SessionTransition.Restore,
            Now.AddMinutes(2),
            TestContext.Current.CancellationToken);

        Assert.Equal(SessionStoreResult.Status.TrainerScheduleConflict, result.Kind);
    }

    [Fact]
    public async Task Create_CancelledToken_DoesNotPersist()
    {
        var seed = await SeedAsync();
        await using var context = _fixture.CreateContext(seed.TrainerId);
        using var source = new CancellationTokenSource();
        source.Cancel();

        var action = () => CreateStore(context).CreateAsync(
            seed.TrainerId,
            seed.ClientId,
            null,
            Start(1),
            60,
            null,
            null,
            null,
            Now,
            source.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(action);
    }

    [Fact]
    public async Task Create_DstFallback_SameLocalDayStillConflicts()
    {
        var seed = await SeedAsync();
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = CreateStore(context);
        var firstOccurrence = new DateTimeOffset(
            2026,
            10,
            25,
            0,
            30,
            0,
            TimeSpan.Zero);
        var secondOccurrence = new DateTimeOffset(
            2026,
            10,
            25,
            1,
            30,
            0,
            TimeSpan.Zero);
        await store.CreateAsync(
            seed.TrainerId,
            seed.ClientId,
            null,
            firstOccurrence,
            30,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);

        var result = await store.CreateAsync(
            seed.TrainerId,
            seed.ClientId,
            null,
            secondOccurrence,
            30,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);

        Assert.Equal(SessionStoreResult.Status.ClientDayConflict, result.Kind);
    }

    [Fact]
    public async Task Get_CrossTenant_ReturnsNull()
    {
        var owner = await SeedAsync();
        var other = await SeedAsync();
        await using var ownerContext = _fixture.CreateContext(owner.TrainerId);
        var created = await CreateStore(ownerContext).CreateAsync(
            owner.TrainerId,
            owner.ClientId,
            null,
            Start(1),
            60,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);
        await using var otherContext = _fixture.CreateContext(other.TrainerId);

        var result = await new SessionQueries(otherContext).GetAsync(
            other.TrainerId,
            created.Session!.Id,
            TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task Transition_CrossTenant_ReturnsNotFound()
    {
        var owner = await SeedAsync();
        var other = await SeedAsync();
        await using var ownerContext = _fixture.CreateContext(owner.TrainerId);
        var created = await CreateStore(ownerContext).CreateAsync(
            owner.TrainerId,
            owner.ClientId,
            null,
            Start(1),
            60,
            null,
            null,
            null,
            Now,
            TestContext.Current.CancellationToken);
        await using var otherContext = _fixture.CreateContext(other.TrainerId);

        var result = await CreateStore(otherContext).TransitionAsync(
            other.TrainerId,
            created.Session!.Id,
            SessionTransition.CancelByTrainer,
            Now.AddMinutes(1),
            TestContext.Current.CancellationToken);

        Assert.Equal(SessionStoreResult.Status.SessionNotFound, result.Kind);
    }

    private async Task<SessionStoreResult> CompleteAsync(
        Guid trainerId,
        Guid sessionId,
        DateTime now)
    {
        await using var context = _fixture.CreateContext(trainerId);
        return await CreateStore(context).TransitionAsync(
            trainerId,
            sessionId,
            SessionTransition.Complete,
            now,
            TestContext.Current.CancellationToken);
    }

    private async Task<Seed> SeedAsync(
        bool clientActive = true,
        int packSessions = 2,
        bool includeSecondClient = false)
    {
        var discriminator = Guid.NewGuid().ToString("N");
        var trainer = ClientPersistenceTestData.CreateTrainer(discriminator);
        var client = ClientPersistenceTestData.CreateClient(
            trainer.Id,
            discriminator,
            clientActive);
        var second = includeSecondClient
            ? ClientPersistenceTestData.CreateClient(
                trainer.Id,
                discriminator + "-2")
            : null;
        var packType = ClientPersistenceTestData.CreatePackType(
            trainer.Id,
            discriminator,
            packSessions);
        var pack = ClientPersistenceTestData.CreatePack(
            trainer.Id,
            client.Id,
            packType,
            new DateOnly(2026, 8, 1),
            null);
        var settings = new TrainerSettingsEntity(trainer.Id, Now);

        var entities = second is null
            ? new object[] { trainer, client, packType, pack, settings }
            : new object[] { trainer, client, second, packType, pack, settings };
        await ClientPersistenceTestData.PersistAsync(
            _fixture,
            trainer.Id,
            entities);

        return new Seed(trainer.Id, client.Id, second?.Id, pack.Id);
    }

    private static SessionStore CreateStore(Infrastructure.Data.PtManagerDbContext context) =>
        new(
            context,
            new TrainerTimeZoneProvider(context),
            new PostgresConstraintTranslator());

    private static DateTimeOffset Start(int days) =>
        new(Now.AddDays(days).AddHours(2));

    private sealed record Seed(
        Guid TrainerId,
        Guid ClientId,
        Guid? SecondClientId,
        Guid PackId);
}
