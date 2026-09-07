using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Jobs;

/// <summary>Valida o tenant persistido de um item interno.</summary>
internal sealed class JobTenantValidator
{
    private readonly PtManagerDbContext _dbContext;

    public JobTenantValidator(PtManagerDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    /// <summary>Confirma que o personal trainer pode executar trabalho de tenant-safe.</summary>
    public async Task<bool> IsAvailableAsync(
        Guid? trainerId,
        CancellationToken cancellationToken)
    {
        if (!trainerId.HasValue || trainerId.Value == Guid.Empty)
            return false;

        // IgnoreQueryFilters é explícito porque o tenant ainda não foi estabelecido.
        // A query restringe-se ao ID persisitido no item, nunca no input HTTP.
        return await _dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user =>
                user.Id == trainerId.Value &&
                user.Role == "trainer" &&
                user.IsActive &&
                !user.IsDeleted)
            .Join(
                _dbContext.TrainerSubscriptions
                    .IgnoreQueryFilters()
                    .AsNoTracking(),
                    user => user.Id,
                    subscription => subscription.TrainerId,
                    (_, subscription) => subscription)
            .AnyAsync(
                subscription => subscription.Status == SubscriptionStatus.Active,
                cancellationToken);

    }
}
