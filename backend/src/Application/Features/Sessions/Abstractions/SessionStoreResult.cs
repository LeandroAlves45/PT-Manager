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

    /// <summary>Nome do cliente da sessão; presente sempre que <see cref="Session"/> está.</summary>
    public string? ClientName { get; }

    private SessionStoreResult(Status kind, Session? session, string? clientName)
    {
        Kind = kind;
        Session = session;
        ClientName = clientName;
    }

    public static SessionStoreResult ForCreated(Session session, string clientName) =>
        WithSession(Status.Created, session, clientName);
    public static SessionStoreResult ForUpdated(Session session, string clientName) =>
        WithSession(Status.Updated, session, clientName);
    public static SessionStoreResult ForAlreadyRequested(Session session, string clientName) =>
        WithSession(Status.AlreadyInRequestedState, session, clientName);
    public static SessionStoreResult For(Status status) => new(status, null, null);

    private static SessionStoreResult WithSession(Status status, Session session, string clientName)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);
        return new SessionStoreResult(status, session, clientName);
    }
}
