using Domain.Entities.Jobs;
using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;

namespace Domain.UnitTests.Entities.Jobs;

public sealed class DurableJobTests
{

    /// <summary>
    /// Cria um job cuja primeira tentativa já está elegível.
    /// </summary>
    private DurableJob CreateDurableJob(
        DateTime? referenceNow = null
    )
    {
        var effectiveNow = referenceNow ?? Now;

        return new DurableJob(
            trainerId: TrainerId,
            jobType: "Send Notification",
            jobVersion: 1,
            payloadJson: """{"notification_id": "test"}""",
            idempotencyKey: "job:test:1",
            correlationId: Guid.Parse("12345678-1234-1234-1234-1234567890ab"),
            scheduledAt: effectiveNow.AddMinutes(-5),
            now: effectiveNow.AddMinutes(-10)
        );
    }

    private DurableJob CreateClaimedJob(
        Guid? leaseOwnerId = null,
        TimeSpan? leaseDuration = null,
        DateTime? now = null
    )
    {
        var effectiveLeaseOwnerId = leaseOwnerId ?? LeaseOwnerA;
        var effectiveLeaseDuration = leaseDuration ?? LeaseDuration;
        var effectiveNow = now ?? Now;

        var job = CreateDurableJob(effectiveNow);
        job.Claim(effectiveLeaseOwnerId, effectiveLeaseDuration, effectiveNow);
        return job;
    }

    private DurableJob CreateFailedJob(
        Guid? leaseOwnerId = null,
        TimeSpan? leaseDuration = null,
        DateTime? now = null
    )
    {
        var effectiveLeaseOwnerId = leaseOwnerId ?? LeaseOwnerA;
        var effectiveLeaseDuration = leaseDuration ?? LeaseDuration;
        var effectiveNow = now ?? Now;
        var job = CreateClaimedJob(effectiveLeaseOwnerId, effectiveLeaseDuration, effectiveNow);
        job.RecordFailure(effectiveLeaseOwnerId, "sanitized error", effectiveNow);
        return job;
    }

    /// <summary>Campos privados usados para testes.</summary>
    private static readonly DateTime Now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid LeaseOwnerA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LeaseOwnerB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private static readonly Guid TrainerId = Guid.Parse("33333333-3333-3333-3333-333333333333");


    [Fact]
    public void Claim_EmptyLeaseOwner_ThrowsDomainException()
    {
        // Arrange
        var job = CreateDurableJob();

        // Act
        var exception = Assert.Throws<DomainException>(() => job.Claim(Guid.Empty, LeaseDuration, Now));

        // Assert
        Assert.Equal("Lease owner is required.", exception.Message);
    }

    [Fact]
    public void Claim_SetsLeaseOwnerAndExpiry()
    {
        // Arrange
        var job = CreateDurableJob();

        // Act
        job.Claim(LeaseOwnerA, LeaseDuration, Now);

        // Assert
        Assert.Equal(LeaseOwnerA, job.LeaseOwnerId);
        Assert.Equal(1, job.Attempts);
        Assert.Equal(Now.Add(LeaseDuration), job.LeaseExpiresAt);
    }

    [Fact]
    public void Claim_ExpiredLease_ReplaceOwnerAndIncrementAttempts()
    {
        var firstClaimAt = Now;
        // Arrange
        var job = CreateClaimedJob(
            LeaseOwnerA,
            TimeSpan.FromMinutes(1),
            firstClaimAt
        );

        var recoveryAt = firstClaimAt.AddMinutes(2);

        // Act
        job.Claim(LeaseOwnerB, TimeSpan.FromMinutes(2), recoveryAt);

        // Assert
        Assert.Equal(
            (LeaseOwnerB, 2, recoveryAt.AddMinutes(2)),
            (job.LeaseOwnerId, job.Attempts, job.LeaseExpiresAt)
        );
    }

    [Fact]
    public void Claim_LeaseStillValid_ThrowsDomainException()
    {
        // Arrange
        var job = CreateClaimedJob(
            LeaseOwnerA,
            LeaseDuration,
            Now
        );

        // Act
        var exception = Assert.Throws<DomainException>(() => job.Claim(
            LeaseOwnerB,
            LeaseDuration,
            Now.AddMinutes(1)
        ));

        // Assert
        Assert.Equal("Job is not claimable for processing.", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Claim_NonPositiveLeaseDuration_ThrowsDomainException(int seconds)
    {
        // Arrange
        var job = CreateDurableJob();
        var leaseDuration = TimeSpan.FromSeconds(seconds);

        // Act
        var exception = Assert.Throws<DomainException>(() => job.Claim(
            Guid.NewGuid(),
            leaseDuration,
            Now
        ));

        // Assert
        Assert.Equal("Lease duration must be positive.", exception.Message);
    }

    [Fact]
    public void IsClaimable_ProcessingWithExpiredLease_ReturnsTrue()
    {
        // Arrange
        var job = CreateClaimedJob(
            LeaseOwnerA,
            LeaseDuration,
            Now.AddMinutes(-5)
        );

        // Act
        var isClaimable = job.IsClaimable(Now);

        // Assert
        Assert.True(isClaimable);
    }

    [Fact]
    public void IsClaimable_ProcessingWithValidLease_ReturnsFalse()
    {
        // Arrange
        var job = CreateClaimedJob(
            LeaseOwnerA,
            LeaseDuration,
            Now
        );

        // Act
        var isClaimable = job.IsClaimable(Now.AddSeconds(30));

        // Assert
        Assert.False(isClaimable);
    }

    [Fact]
    public void IsClaimable_UsesNextAttemptAtWhenPresent()
    {
        // Arrange
        var job = CreateFailedJob(
            LeaseOwnerA,
            LeaseDuration,
            Now
        );
        var nextAttemptAt = Now.AddMinutes(10);
        job.ScheduleRetry(nextAttemptAt, Now.AddMinutes(1));

        // Act
        var isClaimableBeforeNextAttempt = job.IsClaimable(Now.AddMinutes(5));

        // Assert
        Assert.False(isClaimableBeforeNextAttempt);
    }

    [Fact]
    public void RenewLease_ByCurrentOwner_ExtendsExpiry()
    {
        // Arrange
        var job = CreateClaimedJob(
            LeaseOwnerA,
            LeaseDuration,
            Now
        );

        // Act
        var newLeaseDuration = TimeSpan.FromMinutes(3);
        job.RenewLease(LeaseOwnerA, newLeaseDuration, Now.AddMinutes(1));

        // Assert
        Assert.Equal(Now.AddMinutes(4), job.LeaseExpiresAt);
    }

    [Fact]
    public void RenewLease_ByDifferentOwner_ThrowsDomainException()
    {
        // Arrange
        var job = CreateClaimedJob(
            LeaseOwnerA,
            LeaseDuration,
            Now
        );

        // Act
        var exception = Assert.Throws<DomainException>(() => job.RenewLease(
            LeaseOwnerB,
            LeaseDuration,
            Now.AddSeconds(30)
        ));

        // Assert
        Assert.Equal("Only the current lease owner can renew the lease.", exception.Message);
    }

    [Fact]
    public void RenewLease_WhenNotProcessing_ThrowsDomainException()
    {
        // Arrange
        var job = CreateDurableJob();

        // Act
        var exception = Assert.Throws<DomainException>(() => job.RenewLease(
            LeaseOwnerA,
            LeaseDuration,
            Now.AddSeconds(30)
        ));

        // Assert
        Assert.Equal("Only a processing job can renew its lease.", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RenewLease_NonPositiveDuration_ThrowsDomainException(int seconds)
    {
        // Arrange
        var job = CreateClaimedJob(
            LeaseOwnerA,
            LeaseDuration,
            Now
        );

        // Act
        var exception = Assert.Throws<DomainException>(() => job.RenewLease(
            LeaseOwnerA,
            TimeSpan.FromSeconds(seconds),
            Now.AddSeconds(30)
        ));

        // Assert
        Assert.Equal("Lease duration must be positive.", exception.Message);
    }

    [Fact]
    public void RenewLease_BySameOwnerAfterExpiry_ThrowsDomainException()
    {
        // Arrange
        var job = CreateClaimedJob(
            LeaseOwnerA,
            LeaseDuration,
            Now
        );

        // Act
        var exception = Assert.Throws<DomainException>(() => job.RenewLease(
            LeaseOwnerA,
            TimeSpan.FromMinutes(2),
            Now.AddMinutes(2)
        ));

        // Assert
        Assert.Equal("An expired lease cannot be renewed.", exception.Message);
    }

    [Fact]
    public void MarkCompleted_ByDifferentOwner_ThrowsDomainException()
    {
        // Arrange
        var job = CreateClaimedJob(
            LeaseOwnerA,
            LeaseDuration,
            Now
        );

        // Act
        var exception = Assert.Throws<DomainException>(() => job.MarkCompleted(
            LeaseOwnerB,
            Now.AddSeconds(30)
        ));

        // Assert
        Assert.Equal("Only the current lease owner can mark the job as completed.", exception.Message);
    }

    [Fact]
    public void MarkCompleted_BySameOwnerAfterExpiry_ThrowsDomainException()
    {
        // Arrange
        var job = CreateClaimedJob(
            LeaseOwnerA,
            LeaseDuration,
            Now
        );

        // Act
        var exception = Assert.Throws<DomainException>(() => job.MarkCompleted(
            LeaseOwnerA,
            Now.AddMinutes(2)
        ));

        // Assert
        Assert.Equal("An expired lease cannot mark the job as completed.", exception.Message);
    }

    [Fact]
    public void MarkCompleted_ByOwner_ClearsLeaseOwnerAndExpiry()
    {
        // Arrange
        var job = CreateClaimedJob(
            LeaseOwnerA,
            LeaseDuration,
            Now
        );

        // Act
        job.MarkCompleted(LeaseOwnerA, Now.AddSeconds(30));

        // Assert
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Null(job.LeaseOwnerId);
        Assert.Null(job.LeaseExpiresAt);
    }

    [Fact]
    public void RecordFailure_ByDifferentOwner_ThrowsDomainException()
    {
        // Arrange
        var job = CreateClaimedJob(
            LeaseOwnerA,
            LeaseDuration,
            Now
        );
        // Act
        var exception = Assert.Throws<DomainException>(() => job.RecordFailure(
            LeaseOwnerB,
            "sanitized error",
            Now.AddSeconds(30)
        ));

        // Assert
        Assert.Equal("Only the current lease owner can record a failure.", exception.Message);
    }

    [Fact]
    public void RecordFailure_BySameOwnerAfterExpiry_ThrowsDomainException()
    {
        // Arrange
        var job = CreateClaimedJob(
            LeaseOwnerA,
            LeaseDuration,
            Now
        );

        // Act
        var exception = Assert.Throws<DomainException>(() => job.RecordFailure(
            LeaseOwnerA,
            "sanitized error",
            Now.AddMinutes(2)
        ));

        // Assert
        Assert.Equal("An expired lease cannot record a failure.", exception.Message);
    }

    [Fact]
    public void RecordFailure_ByOwner_ClearsLeaseAndStoresError()
    {
        // Arrange
        var job = CreateClaimedJob(
            LeaseOwnerA,
            LeaseDuration,
            Now
        );

        // Act
        job.RecordFailure(LeaseOwnerA, "sanitized error", Now.AddSeconds(30));

        // Assert
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal("sanitized error", job.LastError);
        Assert.Null(job.LeaseOwnerId);
        Assert.Null(job.LeaseExpiresAt);
    }

    [Fact]
    public void ScheduleRetry_DoesNotMutateScheduledAt()
    {
        // Arrange
        var job = CreateFailedJob(
            LeaseOwnerA,
            LeaseDuration,
            Now
        );

        // Act
        var nextAttemptAt = Now.AddMinutes(5);
        job.ScheduleRetry(nextAttemptAt, Now.AddMinutes(1));

        // Assert
        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal(nextAttemptAt, job.NextAttemptAt);
        Assert.Null(job.LeaseOwnerId);
        Assert.Null(job.LeaseExpiresAt);
    }

    [Fact]
    public void ScheduleRetry_ClearsLeaseOwnerAndExpiry()
    {
        // Arrange
        var job = CreateFailedJob(
            LeaseOwnerA,
            LeaseDuration,
            Now
        );

        // Act
        job.ScheduleRetry(Now.AddMinutes(10), Now.AddMinutes(1));

        // Assert
        Assert.Null(job.LeaseOwnerId);
        Assert.Null(job.LeaseExpiresAt);
        Assert.Equal(JobStatus.Pending, job.Status);
    }

    [Fact]
    public void MoveToDeadLetter_ClearsLeaseOwner()
    {
        // Arrange
        var job = CreateFailedJob(
            LeaseOwnerA,
            LeaseDuration,
            Now
        );

        // Act
        job.MoveToDeadLetter(Now.AddSeconds(30));

        // Assert
        Assert.Equal(JobStatus.DeadLetter, job.Status);
        Assert.Null(job.LeaseOwnerId);
        Assert.Null(job.LeaseExpiresAt);
    }
}
