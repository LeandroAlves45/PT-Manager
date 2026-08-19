using System.Linq.Expressions;
using Application.Features.Packs.PackTypes.Abstractions;
using Application.Features.Packs.PackTypes.Dtos;
using Application.Features.Packs.PackTypes.ListPackTypes;
using Application.Pagination;
using Domain.Entities.Billing;
using Infrastructure.Data;
using Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Packs;

/// <summary>Consulta tipos de pack privados sem tracking.</summary>
public sealed class PackTypeQueries : IPackTypeQueries
{
    private readonly PtManagerDbContext _dbContext;

    public PackTypeQueries(PtManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<PackTypeDto?> GetAsync(
        Guid trainerId,
        Guid packTypeId,
        CancellationToken cancellationToken
    ) => BaseQuery(trainerId)
        .Where(pack => pack.Id == packTypeId)
        .Select(Projection)
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<PageResult<PackTypeDto>> ListAsync(
        Guid trainerId,
        string? search,
        PackTypeActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken
    )
    {
        var query = activity switch
        {
            PackTypeActivityFilter.Active => BaseQuery(trainerId)
                .Where(pack => pack.IsActive),
            PackTypeActivityFilter.Archived => BaseQuery(trainerId)
                .Where(pack => !pack.IsActive),
            PackTypeActivityFilter.All => BaseQuery(trainerId),
            _ => throw new ArgumentOutOfRangeException(nameof(activity))
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikeSearchPattern.Build(search);
            query = query.Where(pack => EF.Functions.ILike(
                pack.Name,
                pattern,
                LikeSearchPattern.LikeEscapeCharacter
            ));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(pack => pack.Name)
            .ThenBy(pack => pack.CreatedAt)
            .ThenBy(pack => pack.Id)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(Projection)
            .ToListAsync(cancellationToken);

        return new PageResult<PackTypeDto>(items, totalCount);
    }

    private IQueryable<PackType> BaseQuery(Guid trainerId) =>
        _dbContext.PackTypes
            .AsNoTracking()
            .Where(pack => pack.OwnerTrainerId == trainerId);

    private static Expression<Func<PackType, PackTypeDto>> Projection =>
        pack => new PackTypeDto(
            pack.Id,
            pack.Name,
            pack.SessionCount,
            pack.PriceCents,
            pack.Currency,
            pack.ExpectedDurationDays,
            pack.IsActive,
            pack.CreatedAt,
            pack.UpdatedAt
        );
}
