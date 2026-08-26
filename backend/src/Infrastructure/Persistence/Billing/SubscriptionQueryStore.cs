using Application.Features.Billing.Abstractions;
using Application.Features.Billing.Dtos;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Persistence.Billing;

/// <summary>Consulta a subscription filtrada pelo tenant efetivo.</summary>
internal sealed class SubscriptionQueryStore : ISubscriptionQueryStore
{
    private readonly PtManagerDbContext _dbContext;

    public SubscriptionQueryStore(PtManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<SubscriptionDto?> GetSubscriptionAsync(
        Guid trainerId,
        CancellationToken cancellationToken
    ) => _dbContext.TrainerSubscriptions
        .AsNoTracking()
        .Where(sub => sub.TrainerId == trainerId)
        .Select(sub => new SubscriptionDto(
            sub.Status.Value,
            sub.Tier.Value,
            sub.ClientLimit,
            sub.CurrentClientCount,
            sub.TrialEndsAt
        ))
        .SingleOrDefaultAsync(cancellationToken);
}
