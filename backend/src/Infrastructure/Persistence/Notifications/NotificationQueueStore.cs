using System.Text.Json;
using Application.Common.Abstractions;
using Application.Features.Notifications.Abstractions;
using Domain.Entities.Jobs;
using Domain.Entities.Notifications;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.Persistence.Errors;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Notifications;

/// <summary>Grava a intenção de envio e o job durável na mesma transação.</summary>
internal sealed class NotificationQueueStore : INotificationQueueStore
{
    private const string JobType = "send_notification";
    private const int JobVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly PtManagerDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly PostgresConstraintTranslator _translator;

    public NotificationQueueStore(
        PtManagerDbContext dbContext,
        ITenantContext tenantContext,
        PostgresConstraintTranslator translator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
    }

    public async Task<NotificationQueueStoreResult> EnqueueAsync(
        NotificationQueueRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateTrustedRequest(request);

        var effectiveTenant = _tenantContext.GetRequiredTrainerId();
        if (!effectiveTenant.IsSuccess || effectiveTenant.Value != request.TrainerId)
            // Uma divergência neste ponto significa que um caller interno violou
            // a trust boundary; não é uma falha funcional devolvida ao utilizador.
            throw new InvalidOperationException(
                "Notification personal trainer does not match the established tenant.");

        var idempotencyKey = BuildIdempotencyKey(request);
        var notification = new Notification(
            request.TrainerId,
            request.ClientId,
            new EmailAddress(request.RecipientEmail),
            request.NotificationType,
            request.TemplateKey,
            request.TemplateDataJson,
            request.Now);

        var payload = JsonSerializer.Serialize(
            new SendNotificationJobPayload(notification.Id),
            JsonOptions);

        var job = new DurableJob(
            request.TrainerId,
            JobType,
            JobVersion,
            payload,
            idempotencyKey,
            request.CorrelationId,
            request.ScheduledAt,
            request.Now);

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        var attempt = 0;

        return await strategy.ExecuteAsync(async () =>
        {
            attempt++;

            // Confirma um commit ambíguo antes de repetir a escrita
            if (attempt > 1)
            {
                var existing = await LoadExistingAsync(
                    idempotencyKey,
                    request.TrainerId,
                    cancellationToken);

                if (existing is not null)
                    return existing;
            }

            return await EnqueueOnceAsync(
                request,
                notification,
                job,
                idempotencyKey,
                cancellationToken);
        });
    }

    private async Task<NotificationQueueStoreResult> EnqueueOnceAsync(
        NotificationQueueRequest request,
        Notification notification,
        DurableJob job,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            if (request.ClientId.HasValue)
            {
                var clientExists = await _dbContext.Clients
                    .AsNoTracking()
                    .AnyAsync(
                        client => client.Id == request.ClientId.Value && client.IsActive,
                        cancellationToken);
                if (!clientExists)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return NotificationQueueStoreResult.ClientNotFound();
                }
            }

            _dbContext.Notifications.Add(notification);
            _dbContext.DurableJobs.Add(job);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return NotificationQueueStoreResult.Queued(
                notification.Id,
                notification.CreatedAt);
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);

            if (!_translator.TryTranslate(
                    exception,
                    PersistenceOperation.EnqueueNotification,
                    out var error) ||
                error?.Code != "notification_already_queued")
            {
                throw;
            }

            // As entidades falhadas continuam Added no tracker. Limpá-las evita
            // uma nova tentativa acidental quando a operação existente não é lida.
            _dbContext.ChangeTracker.Clear();
            return await LoadExistingAsync(
                idempotencyKey,
                request.TrainerId,
                cancellationToken) ??
                throw new InvalidOperationException(
                    "Idempotent notification job exists without a readable notification.");
        }
    }

    private async Task<NotificationQueueStoreResult?> LoadExistingAsync(
        string idempotencyKey,
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        var job = await _dbContext.DurableJobs
            .AsNoTracking()
            .Where(candidate => candidate.IdempotencyKey == idempotencyKey)
            .Select(candidate => new { candidate.TrainerId, candidate.Payload })
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null)
            return null;
        if (job.TrainerId != trainerId)
            throw new InvalidOperationException(
                "Idempotency key resolved to another tenant.");

        var payload = JsonSerializer.Deserialize<SendNotificationJobPayload>(
            job.Payload,
            JsonOptions) ?? throw new InvalidOperationException(
                "Stored notification job payload is invalid.");

        var existing = await _dbContext.Notifications
            .AsNoTracking()
            .Where(candidate => candidate.Id == payload.NotificationId)
            .Select(candidate => new { candidate.Id, candidate.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken);

        return existing is null
            ? null
            : NotificationQueueStoreResult.AlreadyQueued(existing.Id, existing.CreatedAt);
    }

    private static string BuildIdempotencyKey(NotificationQueueRequest request) =>
        $"notification:{request.TrainerId:N}:{request.OperationKey.Trim().ToLowerInvariant()}";

    private static void ValidateTrustedRequest(NotificationQueueRequest request)
    {
        if (request.TrainerId == Guid.Empty)
            throw new ArgumentException("Trainer ID is required.", nameof(request));

        if (request.CorrelationId == Guid.Empty)
            throw new ArgumentException("Correlation ID is required.", nameof(request));

        if (request.ScheduledAt < request.Now)
            throw new ArgumentException("Schedule cannot be in the past.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.OperationKey) || request.OperationKey.Length > 100)
            throw new ArgumentException("Operation key is invalid.", nameof(request));
    }

    private sealed record SendNotificationJobPayload(Guid NotificationId);
}
