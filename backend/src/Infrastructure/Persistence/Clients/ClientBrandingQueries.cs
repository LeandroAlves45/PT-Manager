using Application.Features.Clients.Abstractions;
using Application.Features.Clients.Dtos;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Clients;

/// <summary>
/// Resolve o branding do portal do cliente com base no personal trainer do qual ele depende.
/// </summary>
internal sealed class ClientBrandingQueries : IClientBrandingQueries
{
    private readonly PtManagerDbContext _dbContext;

    public ClientBrandingQueries(PtManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<ClientBrandingDto?> GetAsync(
        Guid trainerId,
        Guid clientUserId,
        CancellationToken cancellationToken) =>
        _dbContext.Clients
            .AsNoTracking()
            .Where(client =>
                client.OwnerTrainerId == trainerId &&
                client.UserId == clientUserId &&
                client.IsActive)
            .Join(
                _dbContext.TrainerSettings.AsNoTracking(),
                client => client.OwnerTrainerId,
                settings => settings.TrainerId,
                (client, settings) => new ClientBrandingDto(
                    settings.AppName,
                    settings.LogoUrl,
                    settings.PrimaryColor,
                    settings.BodyColor))
            .SingleOrDefaultAsync(cancellationToken);
}
