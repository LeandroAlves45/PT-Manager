using Application.Common.Abstractions;
using Application.Features.Sessions.Abstractions;
using Application.Features.Sessions.CancelSessionByClient;
using Application.Features.Sessions.CancelSessionByTrainer;
using Application.Features.Sessions.CompleteSession;
using Application.Features.Sessions.MarkSessionNoShow;
using Application.Features.Sessions.RestoreSession;
using Domain.Entities.Sessions;

namespace Application.UnitTests.Features.Sessions;

public sealed class SessionTransitionHandlersTests
{
    private static readonly Guid TrainerId = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly DateTime Now =
        new(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Complete_SelectsComplete()
    {
        var store = new StoreStub();
        var handler = new CompleteSessionHandler(Context(), Clock(), store);

        await handler.HandleAsync(new CompleteSessionCommand(SessionId), Token);

        Assert.Equal(SessionTransition.Complete, store.Transition);
    }

    [Fact]
    public async Task CancelByClient_SelectsClientReason()
    {
        var store = new StoreStub();
        var handler = new CancelSessionByClientHandler(Context(), Clock(), store);

        await handler.HandleAsync(new CancelSessionByClientCommand(SessionId), Token);

        Assert.Equal(SessionTransition.CancelByClient, store.Transition);
    }

    [Fact]
    public async Task CancelByTrainer_SelectsTrainerReason()
    {
        var store = new StoreStub();
        var handler = new CancelSessionByTrainerHandler(Context(), Clock(), store);

        await handler.HandleAsync(new CancelSessionByTrainerCommand(SessionId), Token);

        Assert.Equal(SessionTransition.CancelByTrainer, store.Transition);
    }

    [Fact]
    public async Task MarkNoShow_SelectsNoShow()
    {
        var store = new StoreStub();
        var handler = new MarkSessionNoShowHandler(Context(), Clock(), store);

        await handler.HandleAsync(new MarkSessionNoShowCommand(SessionId), Token);

        Assert.Equal(SessionTransition.MarkNoShow, store.Transition);
    }

    [Fact]
    public async Task Restore_SelectsRestore()
    {
        var store = new StoreStub();
        var handler = new RestoreSessionHandler(Context(), Clock(), store);

        await handler.HandleAsync(new RestoreSessionCommand(SessionId), Token);

        Assert.Equal(SessionTransition.Restore, store.Transition);
    }

    [Theory]
    [InlineData(SessionStoreResult.Status.SessionNotFound, "session_not_found")]
    [InlineData(SessionStoreResult.Status.ClientNotFound, "client_not_found")]
    [InlineData(SessionStoreResult.Status.ClientInactive, "session_client_inactive")]
    [InlineData(SessionStoreResult.Status.PackNotAvailable, "session_pack_not_available")]
    [InlineData(SessionStoreResult.Status.ClientDayConflict, "session_client_day_conflict")]
    [InlineData(SessionStoreResult.Status.TrainerScheduleConflict, "session_schedule_conflict")]
    [InlineData(SessionStoreResult.Status.InvalidState, "session_invalid_state")]
    [InlineData(SessionStoreResult.Status.PackBalanceUnavailable, "session_pack_balance_unavailable")]
    [InlineData(SessionStoreResult.Status.TransitionTooEarly, "session_transition_too_early")]
    [InlineData(SessionStoreResult.Status.StartsAtNotFuture, "validation_failed")]
    public async Task Complete_ExpectedOutcome_MapsStableError(
        SessionStoreResult.Status status,
        string expectedCode)
    {
        var store = new StoreStub { Outcome = SessionStoreResult.For(status) };
        var handler = new CompleteSessionHandler(Context(), Clock(), store);

        var result = await handler.HandleAsync(
            new CompleteSessionCommand(SessionId),
            Token);

        Assert.Equal(expectedCode, result.Error!.Code);
    }

    private static ITenantContext Context() => new TenantStub();
    private static IClock Clock() => new ClockStub();
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class TenantStub : ITenantContext
    {
        public Guid? TrainerId => SessionTransitionHandlersTests.TrainerId;
        public Guid? UserId => Guid.NewGuid();
        public string? Role => "trainer";
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative => false;
    }

    private sealed class ClockStub : IClock
    {
        public DateTime UtcNow => Now;
    }

    private sealed class StoreStub : ISessionStore
    {
        public SessionStoreResult? Outcome { get; init; }
        public SessionTransition? Transition { get; private set; }

        public Task<SessionStoreResult> TransitionAsync(
            Guid trainerId,
            Guid sessionId,
            SessionTransition transition,
            DateTime now,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Transition = transition;
            var session = new Session(
                trainerId,
                Guid.NewGuid(),
                null,
                new DateTimeOffset(now.AddDays(1)),
                60,
                null,
                null,
                null,
                now);

            return Task.FromResult(
                Outcome ?? SessionStoreResult.ForUpdated(session));
        }

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
            CancellationToken cancellationToken) =>
            Task.FromResult(SessionStoreResult.For(
                SessionStoreResult.Status.SessionNotFound));

        public Task<SessionStoreResult> RescheduleAsync(
            Guid trainerId,
            Guid sessionId,
            DateTimeOffset startsAt,
            int durationMinutes,
            string? location,
            DateTime now,
            CancellationToken cancellationToken) =>
            Task.FromResult(SessionStoreResult.For(
                SessionStoreResult.Status.SessionNotFound));

        public Task<SessionStoreResult> ChangePackAsync(
            Guid trainerId,
            Guid sessionId,
            Guid? packId,
            DateTime now,
            CancellationToken cancellationToken) =>
            Task.FromResult(SessionStoreResult.For(
                SessionStoreResult.Status.SessionNotFound));
    }
}
