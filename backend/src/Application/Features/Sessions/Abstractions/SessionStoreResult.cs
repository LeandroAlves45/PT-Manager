using Domain.Entities.Sessions;

namespace Application.Features.Sessions.Abstractions;

/// <summary>Resultado esperado de uma mutação transacional de sessão.</summary>
public sealed class SessionStoreResult
{
    public enum Status
    {
        Created,
        Updated,
        AlreadyInRequestedState,
        SessionNotFound,
        ClientNotFound,
        ClientInactive,
        PackNotAvailable,
        ClientDayConflict,
        TrainerScheduleConflict,
        InvalidState,
        PackBalanceUnavailable,
        TransitionTooEarly,
        StartsAtNotFuture
    }

    public Status Kind { get; }
    public Session? Session { get; }

    private SessionStoreResult(Status kind, Session? session)
    {
        Kind = kind;
        Session = session;
    }

    public static SessionStoreResult ForCreated(Session session) =>
        WithSession(Status.Created, session);
    public static SessionStoreResult ForUpdated(Session session) =>
        WithSession(Status.Updated, session);
    public static SessionStoreResult ForAlreadyRequested(Session session) =>
        WithSession(Status.AlreadyInRequestedState, session);
    public static SessionStoreResult For(Status status) => new(status, null);

    private static SessionStoreResult WithSession(Status status, Session session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new SessionStoreResult(status, session);
    }
}
