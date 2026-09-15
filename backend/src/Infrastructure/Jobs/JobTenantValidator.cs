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
    /// <param name="trainerId">Tenant persistido no item.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <param name="requireActiveSubscription">
    /// Quando false, basta um personal trainer ativo com subscrição em qualquer estado. É o caso
    /// dos avisos de falha de pagamento e de cancelamento e da limpeza de media, que
    /// existem precisamente quando a subscrição deixou de estar ativa.
    /// </param>
    public async Task<bool> IsAvailableAsync(
        Guid? trainerId,
        CancellationToken cancellationToken,
        bool requireActiveSubscription = true)
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
                subscription => !requireActiveSubscription ||
                    subscription.Status == SubscriptionStatus.Active,
                cancellationToken);

    }
}
