using Domain.Entities.Jobs;
using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;

namespace Domain.UnitTests.Entities.Jobs;

public sealed class OutboxMessageTests
{
    /// <summary>Campos privados usados para testes.</summary>
    private static readonly DateTime Now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid LeaseOwnerA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LeaseOwnerB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private static readonly Guid TrainerId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    /// <summary>Cria uma mensagem pendente e imediatamente elegível.</summary>
    private static OutboxMessage CreatePendingMessage(
        DateTime? referenceNow = null
    )
    {
        var now = referenceNow ?? Now;
        return new OutboxMessage(
            trainerId: TrainerId,
            messageType: "payment_confirmed_email",
            payloadJson: """{"payment_id": "test"}""",
            idempotencyKey: "outbox:test:1",
            correlationId: Guid.Parse("44444444-4444-4444-4444-444444444444"),
            now: now
        );
    }

    private static OutboxMessage CreateClaimedMessage(
        Guid? leaseOwnerId = null,
        TimeSpan? leaseDuration = null,
        DateTime? referenceNow = null
    )
    {
        var now = referenceNow ?? Now;
        var effectiveLeaseOwnerId = leaseOwnerId ?? LeaseOwnerA;
        var effectiveLeaseDuration = leaseDuration ?? LeaseDuration;

        var message = CreatePendingMessage(referenceNow: now);
        message.Claim(effectiveLeaseOwnerId, effectiveLeaseDuration, now);
        return message;
    }

    private static OutboxMessage CreateFailedMessage(
        Guid? leaseOwnerId = null,
        TimeSpan? leaseDuration = null,
        DateTime? referenceNow = null
    )
    {
        var now = referenceNow ?? Now;
        var effectiveLeaseOwnerId = leaseOwnerId ?? LeaseOwnerA;
        var effectiveLeaseDuration = leaseDuration ?? LeaseDuration;

        var message = CreateClaimedMessage(
            referenceNow: now
        );
        message.RecordFailure(effectiveLeaseOwnerId, "Sanitized error message", now.AddSeconds(30));
        return message;
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_InvalidIdempotencyKeyWithSpaces_ThrowsDomainException(string invalidIdempotencyKey)
    {
        var now = Now;
        var correlationId = Guid.Parse("55555555-5555-5555-5555-555555555555");

        var exception = Assert.Throws<DomainException>(() =>
            new OutboxMessage(
                trainerId: TrainerId,
                messageType: "payment_confirmed_email",
                payloadJson: """{"payment_id": "test"}""",
                idempotencyKey: invalidIdempotencyKey,
                correlationId: correlationId,
                now: now
            )
        );

        Assert.Equal(
            "Idempotency key is required and must have a max length of 255 characters.",
            exception.Message
        );
    }

    [Fact]
    public void Constructor_InvalidIdempotencyKeyTooLong_ThrowsDomainException()
    {
        var now = Now;
        var correlationId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var invalidIdempotencyKey = new string('a', 256); // 256 characters

        var exception = Assert.Throws<DomainException>(() =>
            new OutboxMessage(
                trainerId: TrainerId,
                messageType: "payment_confirmed_email",
                payloadJson: """{"payment_id": "test"}""",
                idempotencyKey: invalidIdempotencyKey,
                correlationId: correlationId,
                now: now
            )
        );

        Assert.Equal(
            "Idempotency key is required and must have a max length of 255 characters.",
            exception.Message
        );
    }

    [Fact]
    public void Constructor_StartsPendingWithZeroAttempts()
    {
        var message = CreatePendingMessage();

        Assert.Equal(JobStatus.Pending, message.Status);
        Assert.Equal(0, message.Attempts);
        Assert.Equal(Now, message.CreatedAt);
        Assert.Equal(Now, message.UpdatedAt);
    }

    [Fact]
    public void Claim_SetsOwnerExpiryAndIncrementsAttempts()
    {
        var message = CreatePendingMessage();

        message.Claim(LeaseOwnerA, LeaseDuration, Now);

        Assert.Equal(JobStatus.Processing, message.Status);
        Assert.Equal(LeaseOwnerA, message.LeaseOwnerId);
        Assert.Equal(Now.Add(LeaseDuration), message.LeaseExpiresAt);
        Assert.Equal(1, message.Attempts);
    }

    [Fact]
    public void ClaimEmptyLeaseOwner_ThrowsDomainException()
    {
        var message = CreatePendingMessage();

        var exception = Assert.Throws<DomainException>(() =>
            message.Claim(Guid.Empty, LeaseDuration, Now)
        );

        Assert.Equal("Lease owner is required.", exception.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Claim_NonPositiveLeaseDuration_ThrowsDomainException(int seconds)
    {
        var message = CreatePendingMessage();
        var leaseDuration = TimeSpan.FromSeconds(seconds);

        var exception = Assert.Throws<DomainException>(() =>
            message.Claim(LeaseOwnerA, leaseDuration, Now)
        );

        Assert.Equal("Lease duration must be positive.", exception.Message);
    }

    [Fact]
    public void IsClaimable_ProcessingWithExpiredLease_ReturnsTrue()
    {
        // Arrange
        var message = CreateClaimedMessage(
            LeaseOwnerA,
            LeaseDuration,
            Now.AddMinutes(-5)
        );

        // Act
        var isClaimable = message.IsClaimable(Now);

        // Assert
        Assert.True(isClaimable);
    }

    [Fact]
    public void IsClaimable_ProcessingWithValidLease_ReturnsFalse()
    {
        // Arrange
        var message = CreateClaimedMessage(
            LeaseOwnerA,
            LeaseDuration,
            Now.AddSeconds(-30)
        );

        // Act
        var isClaimable = message.IsClaimable(Now);

        // Assert
        Assert.False(isClaimable);
    }

    [Fact]
    public void RenewLease_ByDifferentOwner_ThrowsDomainException()
    {
        var message = CreateClaimedMessage();

        var exception = Assert.Throws<DomainException>(() =>
            message.RenewLease(LeaseOwnerB, LeaseDuration, Now)
        );

        Assert.Equal("Only the current lease owner can renew the lease.", exception.Message);
    }

    [Fact]
    public void RenewLease_BySameOwnerAfterExpiry_ThrowsDomainException()
    {
        var message = CreateClaimedMessage(
            LeaseOwnerA,
            LeaseDuration,
            Now.AddMinutes(-5)
        );

        var exception = Assert.Throws<DomainException>(() =>
            message.RenewLease(LeaseOwnerA, LeaseDuration, Now)
        );

        Assert.Equal("An expired lease cannot be renewed.", exception.Message);
    }

    [Fact]
    public void MarkCompleted_ByDifferentOwner_ThrowsDomainException()
    {
        var message = CreateClaimedMessage();

        var exception = Assert.Throws<DomainException>(() =>
            message.MarkCompleted(LeaseOwnerB, Now)
        );

        Assert.Equal("Only the current lease owner can complete the message.", exception.Message);
    }

    [Fact]
    public void MarkCompleted_BySameOwnerAfterExpiry_ThrowsDomainException()
    {
        var message = CreateClaimedMessage(
            LeaseOwnerA,
            LeaseDuration,
            Now.AddMinutes(-5)
        );

        var exception = Assert.Throws<DomainException>(() =>
            message.MarkCompleted(LeaseOwnerA, Now)
        );

        Assert.Equal("An expired lease cannot complete the message.", exception.Message);
    }

    [Fact]
    public void MarkCompleted_ByOwner_SetsStatusToCompletedAndClearsLease()
    {
        var message = CreateClaimedMessage();

        message.MarkCompleted(LeaseOwnerA, Now.AddSeconds(30));

        Assert.Equal(JobStatus.Completed, message.Status);
        Assert.Equal(Now.AddSeconds(30), message.CompletedAt);
        Assert.Null(message.LeaseOwnerId);
        Assert.Null(message.LeaseExpiresAt);
    }

    [Fact]
    public void RecordFailure_ByDifferentOwner_ThrowsDomainException()
    {
        var message = CreateClaimedMessage();

        var exception = Assert.Throws<DomainException>(() =>
            message.RecordFailure(LeaseOwnerB, "Sanitized error message", Now)
        );

        Assert.Equal("Only the current lease owner can record a failure.", exception.Message);
    }

    [Fact]
    public void RecordFailure_BySameOwnerAfterExpiry_ThrowsDomainException()
    {
        var message = CreateClaimedMessage(
            LeaseOwnerA,
            LeaseDuration,
            Now.AddMinutes(-5)
        );

        var exception = Assert.Throws<DomainException>(() =>
            message.RecordFailure(LeaseOwnerA, "Sanitized error message", Now)
        );

        Assert.Equal("An expired lease cannot record a failure.", exception.Message);
    }

    [Fact]
    public void RecordFailure_ByOwner_StoresSanitizedError()
    {
        // Arrange
        var message = CreateClaimedMessage();

        // Act
        message.RecordFailure(LeaseOwnerA, "Sanitized error message", Now.AddSeconds(30));

        // Assert
        Assert.Equal(JobStatus.Failed, message.Status);
        Assert.Equal("Sanitized error message", message.LastError);
        Assert.Null(message.LeaseOwnerId);
        Assert.Null(message.LeaseExpiresAt);
    }

    [Fact]
    public void ScheduleRetry_FromFailed_ReturnsToPending()
    {
        // Arrange
        var message = CreateFailedMessage();

        // Act
        var nextAttemptAt = Now.AddMinutes(10);
        message.ScheduleRetry(nextAttemptAt, Now.AddMinutes(1));

        // Assert
        Assert.Equal(JobStatus.Pending, message.Status);
        Assert.Equal(nextAttemptAt, message.NextAttemptAt);
    }

    [Fact]
    public void MoveToDeadLetter_FromFailed_ReachesTerminalState()
    {
        // Arrange
        var message = CreateFailedMessage();

        // Act
        message.MoveToDeadLetter(Now.AddMinutes(1));

        // Assert
        Assert.Equal(JobStatus.DeadLetter, message.Status);
    }
}
