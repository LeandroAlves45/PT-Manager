namespace Application.Features.Sessions.ListSessions;

/// <summary>Lista sessões do personal trainer com filtros opcionais.</summary>
public sealed record ListSessionsQuery(
    Guid? ClientId,
    SessionStatusFilter? Status,
    DateTimeOffset? StartsFrom,
    DateTimeOffset? StartsBefore,
    int PageNumber = 1,
    int PageSize = 50
);
