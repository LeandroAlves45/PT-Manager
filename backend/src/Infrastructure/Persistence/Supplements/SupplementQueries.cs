using System.Linq.Expressions;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Features.Supplements.ListSupplements;
using Application.Pagination;
using Domain.Entities.Supplements;
using Infrastructure.Data;
using Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Supplements;

/// <summary>Consulta o catálogo visível ao personal trainer sem tracking.</summary>
internal sealed class SupplementQueries : ISupplementQueries
{
    private readonly PtManagerDbContext _dbContext;

    public SupplementQueries(PtManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<SupplementDto?> GetAsync(
        Guid trainerId,
        Guid supplementId,
        CancellationToken cancellationToken) => BaseVisibleQuery(trainerId)
        .Where(item => item.Id == supplementId)
        .Select(Projection)
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<PageResult<SupplementDto>> ListAsync(
        Guid trainerId,
        string? search,
        SupplementActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = activity switch
        {
            SupplementActivityFilter.Active => BaseVisibleQuery(trainerId)
                .Where(item => item.IsActive),
            SupplementActivityFilter.Archived => BaseVisibleQuery(trainerId)
                .Where(item => item.OwnerTrainerId == trainerId && !item.IsActive),
            SupplementActivityFilter.All => BaseVisibleQuery(trainerId),
            _ => throw new ArgumentOutOfRangeException(nameof(activity))
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikeSearchPattern.Build(search);
            query = query.Where(item =>
                EF.Functions.ILike(item.Name, pattern, LikeSearchPattern.LikeEscapeCharacter) ||
                item.Description != null &&
                EF.Functions.ILike(
                    item.Description, pattern, LikeSearchPattern.LikeEscapeCharacter));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.Name)
            .ThenBy(item => item.OwnerTrainerId == null)
            .ThenBy(item => item.Id)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(Projection)
            .ToListAsync(cancellationToken);

        return new PageResult<SupplementDto>(items, totalCount);
    }

    private IQueryable<Supplement> BaseVisibleQuery(Guid trainerId) =>
        _dbContext.Supplements
            .AsNoTracking()
            .Where(item => item.OwnerTrainerId == trainerId ||
            item.OwnerTrainerId == null && item.IsActive);

    private static Expression<Func<Supplement, SupplementDto>> Projection => item =>
        new SupplementDto(
            item.Id,
            item.OwnerTrainerId.HasValue ? "private" : "global",
            item.Name,
            item.Description,
            item.UnitOfMeasure,
            item.ServingSize,
            item.Timing,
            item.TrainerNotes,
            item.IsActive,
            item.CreatedAt,
            item.UpdatedAt);
}
