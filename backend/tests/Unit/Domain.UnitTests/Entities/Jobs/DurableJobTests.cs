using Domain.Entities.Jobs;
using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;

namespace Domain.UnitTests.Entities.Jobs;

public sealed class DurableJobTests
{
    [Fact]
    public void Claim_ExpiredLease_Succeeds()
    {
        // Arrange: job já reclamado por um worker, cujo lease entretanto expirou
        var job = CreateDurableJob();
        var firstClaimAt = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        job.Claim(TimeSpan.FromMinutes(1), firstClaimAt);
        var recoveryAt = firstClaimAt.AddMinutes(5); // lease de 1 min já expirou

        // Act: outro worker recupera o job (dead worker recovery)
        job.Claim(TimeSpan.FromMinutes(2), recoveryAt);

        // Assert
        Assert.Equal(2, job.Attempts);
        Assert.Equal(JobStatus.Processing, job.Status);
    }

    [Fact]
    public void Claim_LeaseStillValid_ThrowsDomainException()
    {
        // Arrange
        var job = CreateDurableJob();
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var lease = new TimeSpan(0, 2, 0); // 2 minutes lease
        job.Claim(lease, now);

        // Assert
        var exception = Assert.Throws<DomainException>(() => job.Claim(lease, now));
        Assert.Equal("Job is not claimable for processing.", exception.Message);
    }

    [Fact]
    public void RecordFailure_FromProcessing_MovesToFailed()
    {
        // Arrange
        var job = CreateDurableJob();
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var lease = new TimeSpan(0, 2, 0); // 2 minutes lease
        job.Claim(lease, now);

        // Act
        job.RecordFailure("Test failure message", now);

        // Assert
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal(1, job.Attempts);
    }

    [Fact]
    public void ScheduleRetry_FromFailed_RequeuesWithSchedule()
    {
        // Arrange
        var job = CreateDurableJob();
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var lease = new TimeSpan(0, 2, 0); // 2 minutes lease
        job.Claim(lease, now);
        job.RecordFailure("Test failure message", now);

        // Act
        var nextSchedule = now.AddMinutes(5);
        job.ScheduleRetry(nextSchedule, now);

        // Assert
        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal(nextSchedule, job.ScheduledAt);
    }

    [Fact]
    public void MoveToDeadLetter_FromFailed_MovesToTerminalState()
    {
        // Arrange
        var job = CreateDurableJob();
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var lease = new TimeSpan(0, 2, 0); // 2 minutes lease
        job.Claim(lease, now);
        job.RecordFailure("Test failure message", now);

        // Act
        job.MoveToDeadLetter(now);

        // Assert
        Assert.Equal(JobStatus.DeadLetter, job.Status);
    }

    [Fact]
    public void MarkCompleted_FromProcessing_ClearsLease()
    {
        // Arrange
        var job = CreateDurableJob();
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var lease = new TimeSpan(0, 2, 0); // 2 minutes lease
        job.Claim(lease, now);

        // Act
        job.MarkCompleted(now);

        // Assert
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Null(job.LeaseExpiresAt);
    }

    /// <summary>
    /// Helper que cria um job de DurableJob para testes.
    /// </summary>
    private DurableJob CreateDurableJob()
    {
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var scheduleAt = now.AddMinutes(-5);
        var job = new DurableJob(
            Guid.NewGuid(),
            "Test Job",
            1,
            "Test Payload",
            "Test IdempotencyKey",
            Guid.NewGuid(),
            scheduleAt,
            now
        );
        return job;
    }
}
