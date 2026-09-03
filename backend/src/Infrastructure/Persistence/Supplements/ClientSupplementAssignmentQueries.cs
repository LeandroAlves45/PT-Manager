using Application.Features.Supplements;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Features.Supplements.ListSupplementAssignments;
using Application.Pagination;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Supplements;

/// <summary>Consulta atribuições através de joins traduzidos pelo EF Core.</summary>
internal sealed class ClientSupplementAssignmentQueries : IClientSupplementAssignmentQueries
{
    private readonly PtManagerDbContext _dbContext;

    public ClientSupplementAssignmentQueries(PtManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<ClientSupplementAssignmentDto?> GetAsync(
        Guid trainerId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var row = await (
            from assignment in _dbContext.ClientSupplementAssignments.AsNoTracking()
            where assignment.OwnerTrainerId == trainerId && assignment.Id == assignmentId
            join supplement in _dbContext.Supplements.AsNoTracking()
                on assignment.SupplementId equals supplement.Id
            select new { assignment, supplement })
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : row.assignment.ToDto(row.supplement);
    }

    public async Task<PageResult<ClientSupplementAssignmentDto>> ListAsync(
        Guid trainerId,
        Guid? clientId,
        SupplementAssignmentActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query =
            from assignment in _dbContext.ClientSupplementAssignments.AsNoTracking()
            where assignment.OwnerTrainerId == trainerId
            join supplement in _dbContext.Supplements.AsNoTracking()
                on assignment.SupplementId equals supplement.Id
            select new { assignment, supplement };

        query = activity switch
        {
            SupplementAssignmentActivityFilter.Active => query.Where(
                item => item.assignment.IsActive),
            SupplementAssignmentActivityFilter.Inactive => query.Where(
                item => !item.assignment.IsActive),
            SupplementAssignmentActivityFilter.All => query,
            _ => throw new ArgumentOutOfRangeException(nameof(activity))
        };

        if (clientId.HasValue)
            query = query.Where(item => item.assignment.ClientId == clientId.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(item => item.assignment.UpdatedAt)
            .ThenBy(item => item.assignment.Id)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(item => item.assignment.ToDto(item.supplement))
            .ToList();

        return new PageResult<ClientSupplementAssignmentDto>(items, totalCount);
    }

    public Task<MySupplementAssignmentDto?> GetMyAsync(
        Guid trainerId,
        Guid userId,
        Guid assignmentId,
        CancellationToken cancellationToken) => (
            from client in _dbContext.Clients.AsNoTracking()
            where client.OwnerTrainerId == trainerId && client.UserId == userId
            join assignment in _dbContext.ClientSupplementAssignments.AsNoTracking()
                on client.Id equals assignment.ClientId
            where assignment.OwnerTrainerId == trainerId &&
                assignment.IsActive &&
                assignment.Id == assignmentId
            join supplement in _dbContext.Supplements.AsNoTracking()
                on assignment.SupplementId equals supplement.Id
            select new MySupplementAssignmentDto(
                assignment.Id,
                supplement.Id,
                supplement.Name,
                supplement.Description,
                supplement.UnitOfMeasure,
                assignment.ServingSize,
                assignment.Timing,
                assignment.TrainerNotes,
                !supplement.IsActive,
                assignment.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PageResult<MySupplementAssignmentDto>> ListMyActiveAsync(
        Guid trainerId,
        Guid userId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query =
            from client in _dbContext.Clients.AsNoTracking()
            where client.OwnerTrainerId == trainerId && client.UserId == userId
            join assignment in _dbContext.ClientSupplementAssignments.AsNoTracking()
                on client.Id equals assignment.ClientId
            where assignment.OwnerTrainerId == trainerId && assignment.IsActive
            join supplement in _dbContext.Supplements.AsNoTracking()
                on assignment.SupplementId equals supplement.Id
            select new { Assignment = assignment, Supplement = supplement };

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.Supplement.Name)
            .ThenBy(item => item.Assignment.Id)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(item => new MySupplementAssignmentDto(
                item.Assignment.Id,
                item.Supplement.Id,
                item.Supplement.Name,
                item.Supplement.Description,
                item.Supplement.UnitOfMeasure,
                item.Assignment.ServingSize,
                item.Assignment.Timing,
                item.Assignment.TrainerNotes,
                !item.Supplement.IsActive,
                item.Assignment.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PageResult<MySupplementAssignmentDto>(items, totalCount);
    }
}
