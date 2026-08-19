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

    public Task<ClientSupplementAssignmentDto?> GetAsync(
        Guid trainerId,
        Guid assignmentId,
        CancellationToken cancellationToken) => TrainerQuery(trainerId)
        .Where(item => item.Id == assignmentId)
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<PageResult<ClientSupplementAssignmentDto>> ListAsync(
        Guid trainerId,
        Guid? clientId,
        SupplementAssignmentActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = activity switch
        {
            SupplementAssignmentActivityFilter.Active => TrainerQuery(trainerId)
                .Where(item => item.IsActive),
            SupplementAssignmentActivityFilter.Inactive => TrainerQuery(trainerId)
                .Where(item => !item.IsActive),
            SupplementAssignmentActivityFilter.All => TrainerQuery(trainerId),
            _ => throw new ArgumentOutOfRangeException(nameof(activity))
        };

        if (clientId.HasValue)
            query = query.Where(item => item.ClientId == clientId.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.Id)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<ClientSupplementAssignmentDto>(items, totalCount);
    }

    public Task<MySupplementAssignmentDto?> GetMyAsync(
        Guid trainerId,
        Guid userId,
        Guid assignmentId,
        CancellationToken cancellationToken) => ClientQuery(trainerId, userId)
        .Where(item => item.Id == assignmentId)
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<PageResult<MySupplementAssignmentDto>> ListMyActiveAsync(
        Guid trainerId,
        Guid userId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = ClientQuery(trainerId, userId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.SupplementName)
            .ThenBy(item => item.Id)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<MySupplementAssignmentDto>(items, totalCount);
    }

    private IQueryable<ClientSupplementAssignmentDto> TrainerQuery(Guid trainerId) =>
        from assignment in _dbContext.ClientSupplementAssignments.AsNoTracking()
        where assignment.OwnerTrainerId == trainerId
        join supplement in _dbContext.Supplements.AsNoTracking()
            on assignment.SupplementId equals supplement.Id
        select new ClientSupplementAssignmentDto(
            assignment.Id,
            assignment.ClientId,
            supplement.Id,
            supplement.Name,
            supplement.Description,
            supplement.UnitOfMeasure,
            assignment.ServingSize,
            assignment.Timing,
            assignment.TrainerNotes,
            assignment.IsActive,
            !supplement.IsActive,
            assignment.CreatedAt,
            assignment.UpdatedAt);

    private IQueryable<MySupplementAssignmentDto> ClientQuery(
        Guid trainerId,
        Guid userId) =>
        from client in _dbContext.Clients.AsNoTracking()
        where client.OwnerTrainerId == trainerId && client.UserId == userId
        join assignment in _dbContext.ClientSupplementAssignments.AsNoTracking()
            on client.Id equals assignment.ClientId
        where assignment.OwnerTrainerId == trainerId && assignment.IsActive
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
            assignment.UpdatedAt);
}
