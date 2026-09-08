using Application.Common.Abstractions;
using Application.Features.Notifications.Delivery;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Notifications;

/// <summary>Persiste o estado de entrega condicionado pelo lease do durable job.</summary>
internal sealed class NotificationDeliveryStore : INotificationDeliveryStore
{
    private readonly PtManagerDbContext _dbContext;
    private readonly IClock _clock;

    public NotificationDeliveryStore(
        PtManagerDbContext dbContext,
        IClock clock)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<NotificationDeliveryPreparation> PrepareAsync(
        Guid notificationId,
        Guid jobId,
        Guid leaseOwnerId,
        CancellationToken cancellationToken)
    {
        ValidateIds(notificationId, jobId, leaseOwnerId);
        var now = _clock.UtcNow;

        if (!await HasValidLeaseAsync(jobId, leaseOwnerId, now, cancellationToken))
            return NotificationDeliveryPreparation.FromStatus(
                NotificationDeliveryPreparationStatus.LeaseLost);

        var stored = await _dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.Id == notificationId)
            .Select(notification => new
            {
                notification.Id,
                notification.RecipientEmail,
                notification.TemplateKey,
                notification.TemplateData,
                notification.Status
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (stored is null)
            return NotificationDeliveryPreparation.FromStatus(
                NotificationDeliveryPreparationStatus.NotFound);

        if (stored.Status == "sent")
            return NotificationDeliveryPreparation.FromStatus(
                NotificationDeliveryPreparationStatus.AlreadyDelivered);

        if (stored.Status == "failed")
        {
            var requeued = await _dbContext.Notifications
                .Where(notification =>
                    notification.Id == notificationId &&
                    notification.Status == "failed" &&
                    _dbContext.DurableJobs.Any(job =>
                        job.Id == jobId &&
                        job.LeaseOwnerId == leaseOwnerId &&
                        job.Status == JobStatus.Processing &&
                        job.LeaseExpiresAt > now))
                .ExecuteUpdateAsync(update => update
                    .SetProperty(notification => notification.Status, "pending")
                    .SetProperty(notification => notification.UpdatedAt, now),
                cancellationToken);

            if (requeued != 1)
            {
                var leaseValid = await HasValidLeaseAsync(
                    jobId, leaseOwnerId, now, cancellationToken);
                return NotificationDeliveryPreparation.FromStatus(
                    leaseValid
                        ? NotificationDeliveryPreparationStatus.InvalidState
                        : NotificationDeliveryPreparationStatus.LeaseLost);
            }
        }
        else if (stored.Status != "pending")
            return NotificationDeliveryPreparation.FromStatus(
                NotificationDeliveryPreparationStatus.InvalidState);

        return NotificationDeliveryPreparation.Ready(new NotificationDeliveryMessage(
            stored.Id,
            stored.RecipientEmail,
            stored.TemplateKey,
            stored.TemplateData
        ));
    }

    public async Task<NotificationDeliveryMutationStatus> MarkSentAsync(
        Guid notificationId,
        Guid jobId,
        Guid leaseOwnerId,
        CancellationToken cancellationToken)
    {
        ValidateIds(notificationId, jobId, leaseOwnerId);
        var now = _clock.UtcNow;

        var affected = await _dbContext.Notifications
            .Where(notification =>
                notification.Id == notificationId &&
                notification.Status == "pending" &&
                _dbContext.DurableJobs.Any(job =>
                    job.Id == jobId &&
                    job.LeaseOwnerId == leaseOwnerId &&
                    job.Status == JobStatus.Processing &&
                    job.LeaseExpiresAt > now))
            .ExecuteUpdateAsync(update => update
                .SetProperty(notification => notification.Status, "sent")
                .SetProperty(notification => notification.SentAt, now)
                .SetProperty(notification => notification.UpdatedAt, now),
            cancellationToken);

        return affected == 1
            ? NotificationDeliveryMutationStatus.Applied
            : await ClassifyRejectedMutationAsync(
                notificationId,
                jobId,
                leaseOwnerId,
                "sent",
                now,
                cancellationToken);
    }

    public async Task<NotificationDeliveryMutationStatus> MarkFailedAsync(
        Guid notificationId,
        Guid jobId,
        Guid leaseOwnerId,
        string failureCode,
        CancellationToken cancellationToken)
    {
        ValidateIds(notificationId, jobId, leaseOwnerId);
        ValidateFailureCode(failureCode);
        var now = _clock.UtcNow;

        var affected = await _dbContext.Notifications
            .Where(notification =>
                notification.Id == notificationId &&
                notification.Status == "pending" &&
                _dbContext.DurableJobs.Any(job =>
                    job.Id == jobId &&
                    job.LeaseOwnerId == leaseOwnerId &&
                    job.Status == JobStatus.Processing &&
                    job.LeaseExpiresAt > now))
            .ExecuteUpdateAsync(update => update
                .SetProperty(notification => notification.Status, "failed")
                .SetProperty(notification => notification.RetryCount,
                    notification => notification.RetryCount + 1)
                .SetProperty(notification => notification.LastRetryAt, now)
                .SetProperty(notification => notification.ErrorMessage, failureCode)
                .SetProperty(notification => notification.UpdatedAt, now),
            cancellationToken);

        return affected == 1
            ? NotificationDeliveryMutationStatus.Applied
            : await ClassifyRejectedMutationAsync(
                notificationId,
                jobId,
                leaseOwnerId,
                "failed",
                now,
                cancellationToken);
    }

    private async Task<NotificationDeliveryMutationStatus> ClassifyRejectedMutationAsync(
        Guid notificationId,
        Guid jobId,
        Guid leaseOwnerId,
        string expectedStatus,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!await HasValidLeaseAsync(jobId, leaseOwnerId, now, cancellationToken))
            return NotificationDeliveryMutationStatus.LeaseLost;

        var currentStatus = await _dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.Id == notificationId)
            .Select(notification => notification.Status)
            .SingleOrDefaultAsync(cancellationToken);

        return currentStatus == expectedStatus
            ? NotificationDeliveryMutationStatus.AlreadyApplied
            : NotificationDeliveryMutationStatus.InvalidState;
    }

    private Task<bool> HasValidLeaseAsync(
        Guid jobId,
        Guid leaseOwnerId,
        DateTime now,
        CancellationToken cancellationToken) =>
        _dbContext.DurableJobs
            .AsNoTracking()
            .AnyAsync(job =>
                job.Id == jobId &&
                job.LeaseOwnerId == leaseOwnerId &&
                job.Status == JobStatus.Processing &&
                job.LeaseExpiresAt > now,
                cancellationToken);

    private static void ValidateIds(
        Guid notificationId,
        Guid jobId,
        Guid leaseOwnerId)
    {
        if (notificationId == Guid.Empty)
            throw new ArgumentException("Notification ID is required.", nameof(notificationId));

        if (jobId == Guid.Empty)
            throw new ArgumentException("Job ID is required.", nameof(jobId));

        if (leaseOwnerId == Guid.Empty)
            throw new ArgumentException("Lease owner ID is required.", nameof(leaseOwnerId));
    }

    private static void ValidateFailureCode(string failureCode)
    {
        if (string.IsNullOrWhiteSpace(failureCode) ||
            failureCode.Length > 100 ||
            failureCode.Any(character =>
                character is not (>= 'a' and <= 'z') and not (>= '0' and <= '9') and not '_'))
            throw new ArgumentException("Failure code is invalid.", nameof(failureCode));
    }
}
