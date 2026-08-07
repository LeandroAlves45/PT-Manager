using Application.Features.Clients.Abstractions;
using Application.Features.Clients.Dtos;
using Application.Features.Clients.ListClients;
using Application.Pagination;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Clients;

/// <summary>Executa queries projetadas de Clients sob os Global Query Filters.</summary>
internal sealed class ClientQueries : IClientQueries
{
    private const string LikeEscapeCharacter = "\\";

    private readonly PtManagerDbContext _dbContext;

    /// <summary>Inicializa queries no DbContext scoped por tenant.</summary>
    public ClientQueries(PtManagerDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<ClientDetailsDto?> GetDetailsAsync(
        Guid clientId,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var client = await _dbContext.Clients
            .AsNoTracking()
            .Where(client => client.Id == clientId)
            .Select(client =>
            new
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

        var packs = await BuildUsablePacksQuery(client.Id, today)
            .ToListAsync(cancellationToken);

        return new ClientDetailsDto(
            Id: client.Id,
            UserId: client.UserId,
            Name: client.Name,
            ContactEmail: client.ContactEmail,
            Phone: client.Phone,
            BirthDate: client.BirthDate,
            Sex: client.Sex,
            Objective: client.Objective,
            Notes: client.Notes,
            EmergencyContactName: client.EmergencyContactName,
            EmergencyContactPhone: client.EmergencyContactPhone,
            AvatarUrl: client.AvatarUrl,
            IsActive: client.IsActive,
            UsablePacks: packs,
            CreatedAt: client.CreatedAt,
            UpdatedAt: client.UpdatedAt);
    }

    /// <inheritdoc/>
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
            var escapedSearch = EscapeLikePattern(search.Trim());
            var pattern = $"%{escapedSearch}%";
            query = query.Where(client =>
                EF.Functions.ILike(
                    client.Name,
                    pattern,
                    LikeEscapeCharacter) ||
                client.NormalizedContactEmail != null &&
                EF.Functions.ILike(
                    client.NormalizedContactEmail,
                    pattern, LikeEscapeCharacter) ||
                EF.Functions.ILike(client.Phone, pattern, LikeEscapeCharacter));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(client => client.Name)
            .ThenBy(client => client.Id)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(client => new ClientSummaryDto(
                Id: client.Id,
                Name: client.Name,
                ContactEmail: client.ContactEmail,
                Phone: client.Phone,
                BirthDate: client.BirthDate.Value,
                Sex: client.Sex.Value,
                Objective: client.Objective,
                IsActive: client.IsActive,
                CreatedAt: client.CreatedAt,
                UpdatedAt: client.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PageResult<ClientSummaryDto>(items, totalCount);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UsableClientPackDto>> ListUsablePacksAsync(
        Guid clientId,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        return await BuildUsablePacksQuery(clientId, today)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<UsableClientPackDto> BuildUsablePacksQuery(
        Guid clientId,
        DateOnly today)
    {
        return _dbContext.ClientSessionPacks
            .AsNoTracking()
            .Where(pack => pack.ClientId == clientId)
            .Where(pack => pack.SessionsRemaining > 0)
            .Where(pack => pack.ExpirationDate == null || pack.ExpirationDate >= today)
            .OrderByDescending(pack => pack.ExpirationDate.HasValue)
            .ThenBy(pack => pack.ExpirationDate)
            .ThenBy(pack => pack.CreatedAt)
            .ThenBy(pack => pack.Id)
            .Select(pack => new UsableClientPackDto(
                Id: pack.Id,
                PackTypeId: pack.PackTypeId,
                Name: pack.PackName,
                SessionsTotal: pack.SessionsTotal,
                SessionsRemaining: pack.SessionsRemaining,
                PriceCents: pack.PriceCents,
                Currency: pack.Currency,
                PurchaseDate: pack.PurchaseDate,
                ExpirationDate: pack.ExpirationDate));
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }
}


