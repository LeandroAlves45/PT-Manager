using System.Linq.Expressions;
using Application.Features.Sessions.Abstractions;
using Application.Features.Sessions.Dtos;
using Application.Features.Sessions.ListSessions;
using Application.Pagination;
using Domain.Entities.Sessions;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Sessions;

/// <summary>Consulta as sessões no PostgreSQL sem tracking.</summary>
internal sealed class SessionQueries : ISessionQueries
{
    private readonly PtManagerDbContext _dbContext;

    public SessionQueries(PtManagerDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<SessionDto?> GetAsync(
        Guid trainerId,
        Guid sessionId,
        CancellationToken cancellationToken) =>
        BaseQuery(trainerId)
            .Where(session => session.Id == sessionId)
            .Select(Projection)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PageResult<SessionDto>> ListAsync(
        Guid trainerId,
        Guid? clientId,
        SessionStatusFilter? status,
        DateTimeOffset? startsFrom,
        DateTimeOffset? startsBefore,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = BaseQuery(trainerId);

        if (clientId.HasValue)
            query = query.Where(session => session.ClientId == clientId.Value);

        if (status.HasValue)
        {
            var domainStatus = ToDomainStatus(status.Value);
            query = query.Where(session => session.Status == domainStatus);
        }

        if (startsFrom.HasValue)
        {
            var normalized = startsFrom.Value.ToUniversalTime();
            query = query.Where(session => session.StartsAt >= normalized);
        }

        if (startsBefore.HasValue)
        {
            var normalized = startsBefore.Value.ToUniversalTime();
            query = query.Where(session => session.StartsAt < normalized);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(session => session.StartsAt)
            .ThenBy(session => session.Id)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(Projection)
            .ToListAsync(cancellationToken);

        return new PageResult<SessionDto>(items, totalCount);
    }

    private IQueryable<Session> BaseQuery(Guid trainerId) =>
        _dbContext.Sessions
            .AsNoTracking()
            .Where(session => session.OwnerTrainerId == trainerId);

    private static SessionStatus ToDomainStatus(SessionStatusFilter status) =>
        status switch
        {
            SessionStatusFilter.Scheduled => SessionStatus.Scheduled,
            SessionStatusFilter.Completed => SessionStatus.Completed,
            SessionStatusFilter.CancelledByClient => SessionStatus.CancelledByClient,
            SessionStatusFilter.CancelledByTrainer => SessionStatus.CancelledByTrainer,
            SessionStatusFilter.NoShow => SessionStatus.NoShow,
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

    private static Expression<Func<Session, SessionDto>> Projection =>
        session => new SessionDto(
            session.Id,
            session.ClientId,
            session.ClientSessionPackId,
            session.StartsAt,
            session.DurationMinutes,
            session.Location,
            session.SessionType,
            session.Notes,
            session.Status.Value,
            session.StatusChangedAt,
            session.CreatedAt,
            session.UpdatedAt
        );
}
