using System.Text.Json;
using Application.Common.Abstractions;
using Application.Common.Time;
using Application.Features.Notifications.Delivery;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Notifications;

/// <summary>Persiste o estado de entrega condicionado pelo lease do durable job.</summary>
internal sealed class NotificationDeliveryStore : INotificationDeliveryStore
{
    private static readonly JsonSerializerOptions TemplateDataOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    private readonly PtManagerDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ITrainerTimeZoneProvider _timeZoneProvider;

    public NotificationDeliveryStore(
        PtManagerDbContext dbContext,
        IClock clock,
        ITrainerTimeZoneProvider timeZoneProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeZoneProvider = timeZoneProvider ?? throw new ArgumentNullException(nameof(timeZoneProvider));
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
                notification.OwnerTrainerId,
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

        if (SessionReminderTemplates.IsSessionReminder(stored.TemplateKey) &&
            !await SessionReminderStillAppliesAsync(
                stored.OwnerTrainerId, stored.TemplateData, now, cancellationToken))
            return await SkipAsync(notificationId, jobId, leaseOwnerId, now, cancellationToken);

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

    /// <summary>
    /// Confirma, no momento do envio, que o lembrete da véspera ainda se aplica: a sessão
    /// continua marcada, do cliente ativo, no mesmo dia local, e esse dia ainda é amanhã no
    /// fuso do personal trainer. Uma sessão cancelada, apagada, mudada de dia ou que já é
    /// hoje não recebe email.
    /// </summary>
    private async Task<bool> SessionReminderStillAppliesAsync(
        Guid trainerId,
        string? templateDataJson,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // Bloqueado entre o scan e o envio: salta e liberta a chave, para a véspera voltar a
        // aplicar-se se o bloqueio for levantado a tempo.
        if (await _dbContext.TrainerAccessBlocks.AnyAsync(block => block.TrainerId == trainerId, cancellationToken))
            return false;

        var data = ReadTemplateData(templateDataJson);
        // Dados ilegíveis seguem para o gateway, que os recusa como falha permanente.
        if (data is null)
            return true;

        var session = await _dbContext.Sessions
            .AsNoTracking()
            .Where(item =>
                item.Id == data.SessionId &&
                item.OwnerTrainerId == trainerId &&
                !item.IsDeleted &&
                item.Status == SessionStatus.Scheduled &&
                _dbContext.Clients.Any(client =>
                    client.Id == item.ClientId &&
                    client.OwnerTrainerId == trainerId &&
                    client.IsActive))
            .Select(item => new { item.StartsAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (session is null)
            return false;

        var timeZone = await _timeZoneProvider.GetRequiredAsync(trainerId, cancellationToken);
        return LocalDates.ToLocalDate(session.StartsAt, timeZone) == data.SessionLocalDate &&
            LocalDates.Today(now, timeZone) < data.SessionLocalDate;
    }

    /// <summary>
    /// Marca o lembrete como saltado e liberta a chave de idempotência do job, para que uma
    /// nova véspera (sessão que volta a ficar para amanhã) possa voltar a enfileirá-lo.
    /// </summary>
    private async Task<NotificationDeliveryPreparation> SkipAsync(
        Guid notificationId,
        Guid jobId,
        Guid leaseOwnerId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var releasedKeySuffix = ":skipped:" + jobId.ToString("N");
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        var skipped = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            var notifications = await _dbContext.Notifications
                .Where(notification =>
                    notification.Id == notificationId &&
                    notification.Status == "pending" &&
                    _dbContext.DurableJobs.Any(job =>
                        job.Id == jobId &&
                        job.LeaseOwnerId == leaseOwnerId &&
                        job.Status == JobStatus.Processing &&
                        job.LeaseExpiresAt > now))
                .ExecuteUpdateAsync(update => update
                    .SetProperty(notification => notification.Status, "skipped")
                    .SetProperty(notification => notification.UpdatedAt, now),
                cancellationToken);
            if (notifications != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            await _dbContext.DurableJobs
                .Where(job => job.Id == jobId && job.LeaseOwnerId == leaseOwnerId)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(job => job.IdempotencyKey, job => job.IdempotencyKey + releasedKeySuffix),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return true;
        });

        if (skipped)
            return NotificationDeliveryPreparation.FromStatus(
                NotificationDeliveryPreparationStatus.Skipped);

        return NotificationDeliveryPreparation.FromStatus(
            await HasValidLeaseAsync(jobId, leaseOwnerId, now, cancellationToken)
                ? NotificationDeliveryPreparationStatus.InvalidState
                : NotificationDeliveryPreparationStatus.LeaseLost);
    }

    private static SessionReminderTemplateData? ReadTemplateData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var data = JsonSerializer.Deserialize<SessionReminderTemplateData>(json, TemplateDataOptions);
            return data is null || data.SessionId == Guid.Empty ? null : data;
        }
        catch (JsonException)
        {
            return null;
        }
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
