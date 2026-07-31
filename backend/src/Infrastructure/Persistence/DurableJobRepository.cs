using Application.Common.Abstractions;
using Application.Features.Jobs.Abstractions;
using Domain.Entities.Jobs;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Persistence;

public sealed class DurableJobRepository : IDurableJobStore
{
    private readonly PtManagerDbContext _db;
    private readonly IClock _clock;

    public DurableJobRepository(PtManagerDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<DurableJob>> ClaimDueJobsAsync(
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
                FROM durable_jobs
                WHERE (
                        status = 'pending'
                        AND COALESCE(next_attempt_at, scheduled_at) <= @now
                    )
                    OR (
                        status = 'processing'
                        AND lease_expires_at <= @now
                    )
                ORDER BY COALESCE(next_attempt_at, scheduled_at)
                LIMIT @batch_size
                FOR UPDATE SKIP LOCKED
            ), claimed AS (
                UPDATE durable_jobs AS job
                SET status = 'processing',
                    lease_owner_id = @lease_owner_id,
                    lease_expires_at = @lease_expires_at,
                    attempts = job.attempts + 1,
                    updated_at = @now
                FROM candidates
                WHERE job.id = candidates.id
                RETURNING job.*
            )
            SELECT * FROM claimed
            ORDER BY COALESCE(next_attempt_at, scheduled_at);
            """;

        // Uma única instrução mantém seleção e claim na mesma transação implícita.
        return await _db.DurableJobs
            .FromSqlRaw(
                sql,
                new NpgsqlParameter("now", now),
                new NpgsqlParameter("batch_size", batchSize),
                new NpgsqlParameter("lease_owner_id", leaseOwnerId),
                new NpgsqlParameter("lease_expires_at", leaseExpiresAt))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TryRenewLeaseAsync(
        Guid jobId,
        Guid leaseOwnerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        var now = _clock.UtcNow;
        var affected = await _db.Set<DurableJob>()
            .Where(j => j.Id == jobId
                && j.LeaseOwnerId == leaseOwnerId
                && j.Status == JobStatus.Processing
                && j.LeaseExpiresAt > now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.LeaseExpiresAt, j => now + leaseDuration)
                .SetProperty(j => j.UpdatedAt, j => now), cancellationToken);

        // affected == 0 -> o lease mudou de dono. O worker aborta.
        return affected == 1;
    }

    public async Task<bool> TryCompleteAsync(
        Guid jobId,
        Guid leaseOwnerId,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var affected = await _db.Set<DurableJob>()
            .Where(j => j.Id == jobId && j.LeaseOwnerId == leaseOwnerId
                && j.Status == JobStatus.Processing
                && j.LeaseExpiresAt > now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, j => JobStatus.Completed)
                .SetProperty(j => j.LeaseOwnerId, (Guid?)null)
                .SetProperty(j => j.LeaseExpiresAt, (DateTime?)null)
                .SetProperty(j => j.UpdatedAt, j => now), cancellationToken);

        return affected == 1;
    }

    public async Task<bool> TryRecordFailureAsync(
        Guid jobId,
        Guid leaseOwnerId,
        string sanitizedError,
        DateTime? nextAttemptAt,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var affected = await _db.Set<DurableJob>()
            .Where(j => j.Id == jobId && j.LeaseOwnerId == leaseOwnerId
                && j.Status == JobStatus.Processing
                && j.LeaseExpiresAt > now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, j => nextAttemptAt.HasValue ? JobStatus.Pending : JobStatus.DeadLetter)
                .SetProperty(j => j.NextAttemptAt, j => nextAttemptAt)
                .SetProperty(j => j.LastError, j => sanitizedError)
                .SetProperty(j => j.LeaseOwnerId, (Guid?)null)
                .SetProperty(j => j.LeaseExpiresAt, (DateTime?)null)
                .SetProperty(j => j.UpdatedAt, j => now), cancellationToken);

        return affected == 1;
    }
}
