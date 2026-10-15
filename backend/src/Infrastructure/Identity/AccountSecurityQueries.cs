using Application.Features.Authentication.Abstractions;
using Application.Features.Authentication.Dtos;
using Domain.Entities.Identity;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity;

/// <summary>Lê os métodos de entrada da conta: password local e Google ligado.</summary>
internal sealed class AccountSecurityQueries : IAccountSecurityQueries
{
    private readonly PtManagerDbContext _dbContext;

    public AccountSecurityQueries(PtManagerDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public Task<AccountSecurityDto?> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive && !user.IsDeleted)
            .Select(user => new AccountSecurityDto(
                user.Email,
                user.Role,
                !string.IsNullOrWhiteSpace(user.PasswordHash),
                _dbContext.Set<ExternalIdentity>().Any(identity =>
                    identity.UserId == userId &&
                    identity.Provider == ExternalIdentity.GoogleProvider)))
            .SingleOrDefaultAsync(cancellationToken);
}
