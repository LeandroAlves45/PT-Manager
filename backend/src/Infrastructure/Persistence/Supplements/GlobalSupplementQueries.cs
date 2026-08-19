using System.Linq.Expressions;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Features.Supplements.ListGlobalSupplements;
using Application.Pagination;
using Domain.Entities.Supplements;
using Infrastructure.Data;
using Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Supplements;

/// <summary>Consulta globais ignorando filtros apenas neste caso administrativo.</summary>
internal sealed class GlobalSupplementQueries : IGlobalSupplementQueries
{
    private readonly PtManagerDbContext _dbContext;

    public GlobalSupplementQueries(PtManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<GlobalSupplementDto?> GetAsync(
        Guid supplementId,
        CancellationToken cancellationToken) => BaseQuery()
        .Where(item => item.Id == supplementId)
        .Select(Projection)
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<PageResult<GlobalSupplementDto>> ListAsync(
        string? search,
        GlobalSupplementActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = activity switch
        {
            GlobalSupplementActivityFilter.Active => BaseQuery()
                .Where(item => item.IsActive),
            GlobalSupplementActivityFilter.Archived => BaseQuery()
                .Where(item => !item.IsActive),
            GlobalSupplementActivityFilter.All => BaseQuery(),
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
            .ThenBy(item => item.Id)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(Projection)
            .ToListAsync(cancellationToken);

        return new PageResult<GlobalSupplementDto>(items, totalCount);
    }

    private IQueryable<Supplement> BaseQuery() => _dbContext.Supplements
        .IgnoreQueryFilters()
        .AsNoTracking()
        .Where(item => item.OwnerTrainerId == null);

    private static Expression<Func<Supplement, GlobalSupplementDto>> Projection => item =>
        new GlobalSupplementDto(
            item.Id,
            item.CreatedByUserId,
            item.Name,
            item.Description,
            item.UnitOfMeasure,
            item.ServingSize,
            item.Timing,
            item.TrainerNotes,
            item.IsActive,
            item.CreatedAt,
            item.UpdatedAt
        );
}
