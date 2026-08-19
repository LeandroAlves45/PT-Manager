using Application.Features.Clients.Abstractions;
using Application.Features.Clients.Dtos;
using Application.Features.Clients.ListClients;
using Application.Pagination;
using Infrastructure.Data;
using Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Clients;

/// <summary>Executa queries projetadas de Clients sob os Global Query Filters.</summary>
internal sealed class ClientQueries : IClientQueries
{
    private readonly PtManagerDbContext _dbContext;

    public ClientQueries(PtManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public async Task<ClientDetailsDto?> GetDetailsAsync(
        Guid clientId,
        CancellationToken cancellationToken = default)
    {
        var client = await _dbContext.Clients
            .AsNoTracking()
            .Where(client => client.Id == clientId)
            .Select(client => new
            {
                client.Id,
                client.UserId,
                client.Name,
                client.ContactEmail,
                client.Phone,
                BirthDate = client.BirthDate.Value,
                Sex = client.Sex.Value,
                client.Objective,
                client.Notes,
                client.EmergencyContactName,
                client.EmergencyContactPhone,
                client.AvatarUrl,
                client.IsActive,
                client.CreatedAt,
                client.UpdatedAt
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (client is null)
            return null;

        var packs = await BuildUsablePacksQuery(client.Id)
            .ToListAsync(cancellationToken);

        return new ClientDetailsDto(
            client.Id,
            client.UserId,
            client.Name,
            client.ContactEmail,
            client.Phone,
            client.BirthDate,
            client.Sex,
            client.Objective,
            client.Notes,
            client.EmergencyContactName,
            client.EmergencyContactPhone,
            client.AvatarUrl,
            client.IsActive,
            packs,
            client.CreatedAt,
            client.UpdatedAt);
    }

    public async Task<PageResult<ClientSummaryDto>> ListAsync(
        string? search,
        ClientActivityFilter activity,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Clients.AsNoTracking();
        query = activity switch
        {
            ClientActivityFilter.Active => query.Where(client => client.IsActive),
            ClientActivityFilter.Archived => query.Where(client => !client.IsActive),
            ClientActivityFilter.All => query,
            _ => throw new ArgumentOutOfRangeException(nameof(activity))
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikeSearchPattern.Build(search);
            query = query.Where(client =>
                EF.Functions.ILike(
                    client.Name,
                    pattern,
                    LikeSearchPattern.LikeEscapeCharacter) ||
                client.NormalizedContactEmail != null &&
                EF.Functions.ILike(
                    client.NormalizedContactEmail,
                    pattern, LikeSearchPattern.LikeEscapeCharacter) ||
                EF.Functions.ILike(
                    client.Phone, pattern, LikeSearchPattern.LikeEscapeCharacter));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(client => client.Name)
            .ThenBy(client => client.Id)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(client => new ClientSummaryDto(
                client.Id,
                client.Name,
                client.ContactEmail,
                client.Phone,
                client.BirthDate.Value,
                client.Sex.Value,
                client.Objective,
                client.IsActive,
                client.CreatedAt,
                client.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PageResult<ClientSummaryDto>(items, totalCount);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UsableClientPackDto>> ListUsablePacksAsync(
        Guid clientId,
        CancellationToken cancellationToken = default
    ) => await BuildUsablePacksQuery(clientId)
        .ToListAsync(cancellationToken);

    private IQueryable<UsableClientPackDto> BuildUsablePacksQuery(Guid clientId) =>
        _dbContext.ClientSessionPacks
            .AsNoTracking()
            .Where(pack => pack.ClientId == clientId)
            .Where(pack => pack.SessionsRemaining > 0)
            .OrderBy(pack => pack.ExpectedEndDate == null)
            .ThenBy(pack => pack.ExpectedEndDate)
            .ThenBy(pack => pack.CreatedAt)
            .ThenBy(pack => pack.Id)
            .Select(pack => new UsableClientPackDto(
                pack.Id,
                pack.PackTypeId,
                pack.PackName,
                pack.SessionsTotal,
                pack.SessionsRemaining,
                pack.PriceCents,
                pack.Currency,
                pack.PurchaseDate,
                pack.ExpectedEndDate,
                pack.CreatedAt));
}


