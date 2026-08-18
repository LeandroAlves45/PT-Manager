using Application.Features.Assessments;
using Application.Features.Assessments.CheckIns.Abstractions;
using Application.Features.Assessments.CheckIns.Dtos;
using Application.Features.Assessments.CheckIns.ListCheckIns;
using Application.Pagination;
using Domain.Entities.Assessments;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Assessments;

/// <summary>Consulta CheckIns sem tracking e calcula o estado com o dia local recebido.</summary>
internal sealed class CheckInQueries : ICheckInQueries
{
    private readonly PtManagerDbContext _dbContext;

    public CheckInQueries(PtManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<CheckInDto?> GetAsync(
        Guid trainerId,
        Guid checkInId,
        DateOnly localToday,
        CancellationToken cancellationToken
    )
    {
        var checkIn = await BaseQuery(trainerId)
            .SingleOrDefaultAsync(item => item.Id == checkInId, cancellationToken);

        return checkIn?.ToDto(localToday);
    }

    public async Task<PageResult<CheckInDto>> ListAsync(
        Guid trainerId,
        Guid? clientId,
        CheckInStatusFilter? status,
        DateOnly? fromDate,
        DateOnly? toDate,
        DateOnly localToday,
        PageRequest page,
        CancellationToken cancellationToken
    )
    {
        var query = BaseQuery(trainerId);

        if (clientId.HasValue)
            query = query.Where(item => item.ClientId == clientId.Value);

        if (fromDate.HasValue)
            query = query.Where(item => item.CheckInDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(item => item.CheckInDate <= toDate.Value);

        if (status.HasValue)
            query = ApplyStatus(query, status.Value, localToday);

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderByDescending(item => item.CheckInDate)
            .ThenBy(item => item.Id)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        var items = entities
            .Select(item => item.ToDto(localToday))
            .ToList();

        return new PageResult<CheckInDto>(items, totalCount);
    }

    public async Task<CheckInDto?> GetMyDueAsync(
        Guid trainerId,
        Guid userId,
        DateOnly localToday,
        CancellationToken cancellationToken
    )
    {
        var checkIn = await BaseQuery(trainerId)
            .Where(item => item.CheckInDate == localToday &&
                !item.RespondedAt.HasValue &&
                !item.CancelledAt.HasValue)
            .Join(
                _dbContext.Clients.AsNoTracking().Where(
                    client => client.OwnerTrainerId == trainerId &&
                        client.UserId == userId && client.IsActive),
                item => item.ClientId,
                client => client.Id,
                (item, client) => item)
            .SingleOrDefaultAsync(cancellationToken);

        return checkIn?.ToDto(localToday);
    }

    private IQueryable<CheckIn> BaseQuery(Guid trainerId) =>
        _dbContext.CheckIns
            .AsNoTracking()
            .Where(item => item.OwnerTrainerId == trainerId);

    private static IQueryable<CheckIn> ApplyStatus(
        IQueryable<CheckIn> query,
        CheckInStatusFilter status,
        DateOnly localToday) => status switch
        {
            CheckInStatusFilter.Cancelled => query.Where(
                item => item.CancelledAt.HasValue),
            CheckInStatusFilter.Answered => query.Where(
                item => !item.CancelledAt.HasValue && item.RespondedAt.HasValue),
            CheckInStatusFilter.Missed => query.Where(
                item => !item.CancelledAt.HasValue &&
                    !item.RespondedAt.HasValue &&
                    item.CheckInDate < localToday),
            CheckInStatusFilter.Scheduled => query.Where(
                item => !item.CancelledAt.HasValue &&
                    !item.RespondedAt.HasValue &&
                    item.CheckInDate >= localToday),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
}
