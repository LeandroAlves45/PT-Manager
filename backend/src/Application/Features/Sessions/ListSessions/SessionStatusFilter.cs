namespace Application.Features.Sessions.ListSessions;

/// <summary>Estados aceites como filtro opcional de listagem.</summary>
public enum SessionStatusFilter
{
    Scheduled,
    Completed,
    CancelledByClient,
    CancelledByTrainer,
    NoShow
}
