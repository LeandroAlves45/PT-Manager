using Application.Features.ClientPortal.Abstractions;
using Application.Features.ClientPortal.Dtos;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.ClientPortal;

/// <summary>
/// Projeta o perfil do cliente autenticado.
/// </summary>
internal sealed class MyProfileQueries : IMyProfileQueries
{
    private readonly PtManagerDbContext _dbContext;

    public MyProfileQueries(PtManagerDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public Task<MyProfileDto?> GetAsync(
        Guid trainerId,
        Guid clientUserId,
        CancellationToken cancellationToken) =>
        _dbContext.Clients
            .AsNoTracking()
            .Where(client =>
                client.OwnerTrainerId == trainerId &&
                client.UserId == clientUserId &&
                client.IsActive)
            .Select(client => new MyProfileDto(
                client.Name,
                client.ContactEmail,
                client.Phone,
                client.BirthDate.Value,
                client.Sex.Value,
                client.EmergencyContactName,
                client.EmergencyContactPhone,
                client.AvatarUrl,
                client.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
}
