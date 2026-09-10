using Application.Features.Billing.Notifications;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Billing;

/// <summary>Obtêm o PII apenas no scope de entrega e não a persiste na outbox.</summary>
internal sealed class BillingNotificationRecipientStore : IBillingNotificationRecipientStore
{
    private readonly PtManagerDbContext _dbContext;

    public BillingNotificationRecipientStore(PtManagerDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public Task<string?> GetEmailAsync(
        Guid trainerId,
        CancellationToken cancellationToken) => _dbContext.Users
        .AsNoTracking()
        .Where(user => user.Id == trainerId && user.Role == "trainer" &&
            user.IsActive && !user.IsDeleted)
        .Select(user => user.Email)
        .SingleOrDefaultAsync(cancellationToken);
}
