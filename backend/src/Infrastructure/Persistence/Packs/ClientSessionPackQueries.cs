using System.Linq.Expressions;
using Application.Features.Packs.ClientSessionPacks.Abstractions;
using Application.Features.Packs.ClientSessionPacks.Dtos;
using Application.Features.Packs.ClientSessionPacks.ListClientSessionPacks;
using Application.Pagination;
using Domain.Entities.Billing;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Packs;

/// <summary>Consulta packs atribuídos sem tracking.</summary>
public sealed class ClientSessionPackQueries : IClientSessionPackQueries
{
    private readonly PtManagerDbContext _dbContext;

    public ClientSessionPackQueries(PtManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<ClientSessionPackDto?> GetAsync(
        Guid trainerId,
        Guid packId,
        CancellationToken cancellationToken
    ) => BaseQuery(trainerId)
        .Where(pack => pack.Id == packId)
        .Select(Projection)
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<PageResult<ClientSessionPackDto>> ListAsync(
        Guid trainerId,
        Guid? clientId,
        ClientSessionPackActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken
    )
    {
        var query = activity switch
        {
            ClientSessionPackActivityFilter.Usable => BaseQuery(trainerId)
                .Where(pack => pack.SessionsRemaining > 0),
            ClientSessionPackActivityFilter.Completed => BaseQuery(trainerId)
                .Where(pack => pack.SessionsRemaining == 0),
            ClientSessionPackActivityFilter.All => BaseQuery(trainerId),
            _ => throw new ArgumentOutOfRangeException(nameof(activity))
        };

        if (clientId.HasValue)
            query = query.Where(pack => pack.ClientId == clientId.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await ApplyStableOrder(query)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(Projection)
            .ToListAsync(cancellationToken);

        return new PageResult<ClientSessionPackDto>(items, totalCount);
    }

    public async Task<IReadOnlyList<ClientSessionPackDto>> ListUsableAsync(
        Guid trainerId,
        Guid clientId,
        CancellationToken cancellationToken
    ) => await ApplyStableOrder(
            BaseQuery(trainerId)
                .Where(pack => pack.ClientId == clientId)
                .Where(pack => pack.SessionsRemaining > 0)
        )
        .Select(Projection)
        .ToListAsync(cancellationToken);

    private IQueryable<ClientSessionPack> BaseQuery(Guid trainerId) =>
        _dbContext.ClientSessionPacks
            .AsNoTracking()
            .Where(pack => pack.OwnerTrainerId == trainerId);

    private static IOrderedQueryable<ClientSessionPack> ApplyStableOrder(
        IQueryable<ClientSessionPack> query
    ) => query
        .OrderBy(pack => pack.ExpectedEndDate == null)
        .ThenBy(pack => pack.ExpectedEndDate)
        .ThenBy(pack => pack.CreatedAt)
        .ThenBy(pack => pack.Id);

    private static Expression<Func<ClientSessionPack, ClientSessionPackDto>> Projection =>
        pack => new ClientSessionPackDto(
            pack.Id,
            pack.ClientId,
            pack.PackTypeId,
            pack.PackName,
            pack.SessionsTotal,
            pack.SessionsRemaining,
            pack.PriceCents,
            pack.Currency,
            pack.PurchaseDate,
            pack.ExpectedEndDate,
            pack.SessionsRemaining == 0,
            pack.CompletedAt,
            pack.IsDeleted,
            pack.CreatedAt,
            pack.UpdatedAt
        );
}
