using Application.Common.Abstractions;
using Application.Features.Sessions.Abstractions;
using Application.Features.Sessions.CompleteSession;
using Application.Features.Sessions.CreateSession;
using Application.Features.Sessions.Dtos;
using Application.Features.Sessions.GetSession;
using Application.Features.Sessions.ListSessions;
using Application.Pagination;
using Domain.Entities.Sessions;

namespace Application.UnitTests.Features.Sessions;

public sealed class SessionHandlersTests
{
    private static readonly Guid TrainerId = Guid.NewGuid();
    private static readonly Guid ClientId = Guid.NewGuid();
    private static readonly DateTime Now =
        new(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Create_Trainer_UsesTenantAndNormalizesStart()
    {
        var store = new StoreStub();
        var handler = new CreateSessionHandler(
            new CreateSessionCommandValidator(new ClockStub(Now)),
            new TenantStub(TrainerId, "trainer"),
            new ClockStub(Now),
            store);

        var result = await handler.HandleAsync(
            new CreateSessionCommand(
                ClientId,
                null,
                new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.FromHours(2)),
                60,
                null,
                null,
                null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(TrainerId, store.TrainerId);
        Assert.Equal(TimeSpan.Zero, store.StartsAt.Offset);
    }

    [Theory]
    [InlineData("client")]
    [InlineData("superuser")]
    public async Task Create_NonTrainer_ReturnsForbiddenWithoutWrite(string role)
    {
        var store = new StoreStub();
        var handler = new CreateSessionHandler(
            new CreateSessionCommandValidator(new ClockStub(Now)),
            new TenantStub(TrainerId, role),
            new ClockStub(Now),
            store);

        var result = await handler.HandleAsync(
            ValidCreate(),
            TestContext.Current.CancellationToken);

        Assert.Equal("session_trainer_only", result.Error!.Code);
        Assert.Equal(0, store.CreateCalls);
    }

    [Fact]
    public async Task Create_InactiveClient_MapsConflict()
    {
        var store = new StoreStub
        {
            Outcome = SessionStoreResult.For(SessionStoreResult.Status.ClientInactive)
        };
        var handler = new CreateSessionHandler(
            new CreateSessionCommandValidator(new ClockStub(Now)),
            new TenantStub(TrainerId, "trainer"),
            new ClockStub(Now),
            store);

        var result = await handler.HandleAsync(
            ValidCreate(),
            TestContext.Current.CancellationToken);

        Assert.Equal("session_client_inactive", result.Error!.Code);
    }

    [Fact]
    public async Task Complete_TooEarly_MapsConflict()
    {
        var store = new StoreStub
        {
            Outcome = SessionStoreResult.For(SessionStoreResult.Status.TransitionTooEarly)
        };
        var handler = new CompleteSessionHandler(
            new TenantStub(TrainerId, "trainer"),
            new ClockStub(Now),
            store);

        var result = await handler.HandleAsync(
            new CompleteSessionCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.Equal("session_transition_too_early", result.Error!.Code);
        Assert.Equal(SessionTransition.Complete, store.Transition);
    }

    [Fact]
    public async Task Get_CrossTenantShape_ReturnsNotFound()
    {
        var handler = new GetSessionHandler(
            new TenantStub(TrainerId, "trainer"),
            new QueryStub());

        var result = await handler.HandleAsync(
            new GetSessionQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.Equal("session_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task List_PropagatesFiltersAndPagination()
    {
        var queries = new QueryStub();
        var handler = new ListSessionsHandler(
            new ListSessionsQueryValidator(),
            new TenantStub(TrainerId, "trainer"),
            queries);
        var from = new DateTimeOffset(Now);

        var result = await handler.HandleAsync(
            new ListSessionsQuery(
                ClientId,
                SessionStatusFilter.Scheduled,
                from,
                from.AddDays(1),
                2,
                25),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new PageRequest(2, 25), queries.Page);
        Assert.Equal(ClientId, queries.ClientId);
    }

    private static CreateSessionCommand ValidCreate() => new(
        ClientId,
        null,
        new DateTimeOffset(Now.AddDays(1)),
        60,
        null,
        null,
        null);

    private sealed class ClockStub(DateTime now) : IClock
    {
        public DateTime UtcNow { get; } = now;
    }

    private sealed class TenantStub(Guid? trainerId, string? role) : ITenantContext
    {
        public Guid? TrainerId { get; } = trainerId;
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? Role { get; } = role;
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative => false;
    }

    private sealed class StoreStub : ISessionStore
    {
        public SessionStoreResult? Outcome { get; init; }
        public int CreateCalls { get; private set; }
        public Guid TrainerId { get; private set; }
        public DateTimeOffset StartsAt { get; private set; }
        public SessionTransition? Transition { get; private set; }

        public Task<SessionStoreResult> CreateAsync(
            Guid trainerId,
            Guid clientId,
            Guid? packId,
            DateTimeOffset startsAt,
            int durationMinutes,
            string? location,
            string? sessionType,
            string? notes,
            DateTime now,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CreateCalls++;
            TrainerId = trainerId;
            StartsAt = startsAt;

            return Task.FromResult(Outcome ?? SessionStoreResult.ForCreated(
                new Session(
                    trainerId,
                    clientId,
                    packId,
                    startsAt,
                    durationMinutes,
                    location,
                    sessionType,
                    notes,
                    now)));
        }

        public Task<SessionStoreResult> TransitionAsync(
            Guid trainerId,
            Guid sessionId,
            SessionTransition transition,
            DateTime now,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Transition = transition;
            return Task.FromResult(Outcome ??
                SessionStoreResult.For(SessionStoreResult.Status.SessionNotFound));
        }

        public Task<SessionStoreResult> RescheduleAsync(
            Guid trainerId,
            Guid sessionId,
            DateTimeOffset startsAt,
            int durationMinutes,
            string? location,
            DateTime now,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Outcome ??
                SessionStoreResult.For(SessionStoreResult.Status.SessionNotFound));
        }

        public Task<SessionStoreResult> ChangePackAsync(
            Guid trainerId,
            Guid sessionId,
            Guid? packId,
            DateTime now,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Outcome ??
                SessionStoreResult.For(SessionStoreResult.Status.SessionNotFound));
        }
    }

    private sealed class QueryStub : ISessionQueries
    {
        public Guid? ClientId { get; private set; }
        public PageRequest? Page { get; private set; }

        public Task<SessionDto?> GetAsync(
            Guid trainerId,
            Guid sessionId,
            CancellationToken cancellationToken) =>
            Task.FromResult<SessionDto?>(null);

        public Task<PageResult<SessionDto>> ListAsync(
            Guid trainerId,
            Guid? clientId,
            SessionStatusFilter? status,
            DateTimeOffset? startsFrom,
            DateTimeOffset? startsBefore,
            PageRequest page,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClientId = clientId;
            Page = page;
            return Task.FromResult(new PageResult<SessionDto>([], 0));
        }
    }
}
