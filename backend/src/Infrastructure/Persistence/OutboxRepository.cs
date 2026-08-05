using Application.Common.Abstractions;
using Application.Features.Jobs.Abstractions;
using Domain.Entities.Jobs;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Persistence;

public sealed class OutboxRepository : IOutboxStore
{
    private readonly PtManagerDbContext _db;
    private readonly IClock _clock;

    public OutboxRepository(PtManagerDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(
        TimeSpan leaseDuration,
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        var now = _clock.UtcNow;
        var leaseOwnerId = Guid.NewGuid();
        var leaseExpiresAt = now.Add(leaseDuration);

        const string sql = """
            WITH candidates AS (
                SELECT id
                FROM outbox_messages
                WHERE (
                        status = 'pending'
                        AND COALESCE(next_attempt_at, created_at) <= @now
                    )
                    OR (
                        status = 'processing'
                        AND lease_expires_at <= @now
                    )
                ORDER BY COALESCE(next_attempt_at, created_at)
                LIMIT @batch_size
                FOR UPDATE SKIP LOCKED
            ), claimed AS (
                UPDATE outbox_messages AS message
                SET status = 'processing',
                    lease_owner_id = @lease_owner_id,
                    lease_expires_at = @lease_expires_at,
                    attempts = message.attempts + 1,
                    updated_at = @now
                FROM candidates
                WHERE message.id = candidates.id
                RETURNING message.*
            )
            SELECT * FROM claimed
            ORDER BY COALESCE(next_attempt_at, created_at);
            """;

        return await _db.OutboxMessages
            .FromSqlRaw(sql,
                new NpgsqlParameter("now", now),
                new NpgsqlParameter("batch_size", batchSize),
                new NpgsqlParameter("lease_owner_id", leaseOwnerId),
                new NpgsqlParameter("lease_expires_at", leaseExpiresAt))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TryRenewLeaseAsync(
        Guid messageId,
        Guid leaseOwnerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        var now = _clock.UtcNow;
        var leaseExpiresAt = now.Add(leaseDuration);

        var affected = await _db.OutboxMessages
            .Where(message =>
                message.Id == messageId
                && message.LeaseOwnerId == leaseOwnerId
                && message.Status == JobStatus.Processing
                && message.LeaseExpiresAt > now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.LeaseExpiresAt, leaseExpiresAt)
                .SetProperty(m => m.UpdatedAt, now), cancellationToken);

        return affected == 1;
    }

    public async Task<bool> TryCompleteAsync(
        Guid messageId,
        Guid leaseOwnerId,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var affected = await _db.Set<OutboxMessage>()
            .Where(m => m.Id == messageId
                && m.LeaseOwnerId == leaseOwnerId
                && m.Status == JobStatus.Processing
                && m.LeaseExpiresAt > now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, JobStatus.Completed)
                .SetProperty(m => m.LeaseOwnerId, (Guid?)null)
                .SetProperty(m => m.LeaseExpiresAt, (DateTime?)null)
                .SetProperty(m => m.CompletedAt, now)
                .SetProperty(m => m.UpdatedAt, now), cancellationToken);

        return affected == 1;
    }

    public async Task<bool> TryRecordFailureAsync(
        Guid messageId,
        Guid leaseOwnerId,
        string sanitizedError,
        DateTime? nextAttemptAt,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        if (nextAttemptAt.HasValue && nextAttemptAt.Value <= now)
            throw new ArgumentOutOfRangeException(
                nameof(nextAttemptAt),
                "Next attempt time must be in the future.");

        var affected = await _db.Set<OutboxMessage>()
            .Where(m => m.Id == messageId
                && m.LeaseOwnerId == leaseOwnerId
                && m.Status == JobStatus.Processing
                && m.LeaseExpiresAt > now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, nextAttemptAt.HasValue ? JobStatus.Pending : JobStatus.DeadLetter)
                .SetProperty(m => m.NextAttemptAt, nextAttemptAt)
                .SetProperty(m => m.LastError, sanitizedError)
                .SetProperty(m => m.LeaseOwnerId, (Guid?)null)
                .SetProperty(m => m.LeaseExpiresAt, (DateTime?)null)
                .SetProperty(m => m.UpdatedAt, now), cancellationToken);

        return affected == 1;
    }
}
