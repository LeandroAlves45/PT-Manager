using Domain.ValueObjects;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.IntegrationTests.Jobs;

[Collection(PostgresCollection.Name)]
public sealed class OutboxRepositoryTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 8, 5, 13, 0, 0, DateTimeKind.Utc);

    private readonly PostgresContainerFixture _fixture;

    public OutboxRepositoryTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        await _fixture.ExecuteSqlAsync(
            "TRUNCATE TABLE outbox_messages",
            TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task SaveChanges_WhenMessageIsPending_PersistsNullCompletedAt()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var message = IntegrationTestData.Message(Now);
        await using var context = _fixture.CreateAdministrativeContext();
        context.OutboxMessages.Add(message);

        // Act
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();
        var stored = await context.OutboxMessages
            .SingleAsync(m => m.Id == message.Id, cancellationToken);

        // Assert
        Assert.Null(stored.CompletedAt);
    }

    [Fact]
    public async Task ClaimPendingAsync_WhenTwoWorkersRunConcurrently_ReturnsDisjointSets()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedAsync(
            cancellationToken,
            IntegrationTestData.Message(Now.AddMinutes(-4)),
            IntegrationTestData.Message(Now.AddMinutes(-3)),
            IntegrationTestData.Message(Now.AddMinutes(-2)),
            IntegrationTestData.Message(Now.AddMinutes(-1)));

        await using var contextA = _fixture.CreateAdministrativeContext();
        await using var contextB = _fixture.CreateAdministrativeContext();
        var workerA = new OutboxRepository(contextA, new TestClock(Now));
        var workerB = new OutboxRepository(contextB, new TestClock(Now));

        // Act
        var results = await Task.WhenAll(
            workerA.ClaimPendingAsync(TimeSpan.FromMinutes(5), 2, cancellationToken),
            workerB.ClaimPendingAsync(TimeSpan.FromMinutes(5), 2, cancellationToken));
        var overlapCount = results[0].Select(message => message.Id)
            .Intersect(results[1].Select(message => message.Id))
            .Count();

        // Assert
        Assert.Equal((4, 0), (results.Sum(set => set.Count), overlapCount));
    }

    [Fact]
    public async Task ClaimPendingAsync_WhenLeaseExpired_ReclaimsAbandonedMessage()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var message = IntegrationTestData.Message(Now.AddMinutes(-1));
        await SeedAsync(cancellationToken, message);
        var clock = new TestClock(Now);

        await using var firstContext = _fixture.CreateAdministrativeContext();
        var firstWorker = new OutboxRepository(firstContext, clock);
        await firstWorker.ClaimPendingAsync(TimeSpan.FromMinutes(5), 1, cancellationToken);
        clock.Advance(TimeSpan.FromMinutes(6));

        await using var secondContext = _fixture.CreateAdministrativeContext();
        var secondWorker = new OutboxRepository(secondContext, clock);

        // Act
        var reclaimed = await secondWorker.ClaimPendingAsync(
            TimeSpan.FromMinutes(5), 1, cancellationToken);

        // Assert
        Assert.Equal(message.Id, Assert.Single(reclaimed).Id);
    }

    [Fact]
    public async Task TryCompleteAsync_WhenLeaseIsValid_SetsCompletedAt()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var message = IntegrationTestData.Message(Now);
        await SeedAsync(cancellationToken, message);
        var clock = new TestClock(Now);

        await using var context = _fixture.CreateAdministrativeContext();
        var repository = new OutboxRepository(context, clock);
        var claimed = Assert.Single(await repository.ClaimPendingAsync(
            TimeSpan.FromMinutes(5), 1, cancellationToken));

        // Act
        var completed = await repository.TryCompleteAsync(
            claimed.Id, claimed.LeaseOwnerId!.Value, cancellationToken);
        context.ChangeTracker.Clear();
        var stored = await context.OutboxMessages
            .SingleAsync(value => value.Id == message.Id, cancellationToken);

        // Assert
        Assert.Equal(
            (true, JobStatus.Completed, (DateTime?)Now, (Guid?)null, (DateTime?)null),
            (completed, stored.Status, stored.CompletedAt, stored.LeaseOwnerId, stored.LeaseExpiresAt));
    }

    [Fact]
    public async Task TryCompleteAsync_WhenLeaseIsExpired_ReturnsFalse()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var message = IntegrationTestData.Message(Now);
        await SeedAsync(cancellationToken, message);
        var clock = new TestClock(Now);

        await using var context = _fixture.CreateAdministrativeContext();
        var repository = new OutboxRepository(context, clock);
        var claimed = Assert.Single(await repository.ClaimPendingAsync(
            TimeSpan.FromMinutes(5), 1, cancellationToken));
        clock.Advance(TimeSpan.FromMinutes(6));

        // Act
        var completed = await repository.TryCompleteAsync(
            claimed.Id, claimed.LeaseOwnerId!.Value, cancellationToken);


        // Assert
        Assert.False(completed);
    }

    [Theory]
    [InlineData(true, "pending")]
    [InlineData(false, "dead_letter")]
    public async Task TryRecordFailureAsync_WhenLeaseIsValid_SetsExpectedState(
        bool retry,
        string expectedStatus)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var message = IntegrationTestData.Message(Now);
        await SeedAsync(cancellationToken, message);
        var clock = new TestClock(Now);

        await using var context = _fixture.CreateAdministrativeContext();
        var repository = new OutboxRepository(context, clock);
        var claimed = Assert.Single(await repository.ClaimPendingAsync(
            TimeSpan.FromMinutes(5), 1, cancellationToken));

        var recorded = await repository.TryRecordFailureAsync(
            claimed.Id,
            claimed.LeaseOwnerId!.Value,
            "delivery failed",
            retry ? Now.AddMinutes(10) : null,
            cancellationToken);
        context.ChangeTracker.Clear();
        var stored = await context.OutboxMessages
            .SingleAsync(value => value.Id == message.Id, cancellationToken);

        // Assert
        Assert.Equal(
            (true, expectedStatus),
            (recorded, stored.Status.Value));
    }

    [Fact]
    public async Task SaveChanges_WhenIdempotencyKeyIsDuplicate_ThrowsDbUpdateException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var key = Guid.NewGuid().ToString("N");
        using var context = _fixture.CreateAdministrativeContext();
        context.OutboxMessages.AddRange(
            IntegrationTestData.Message(Now, key),
            IntegrationTestData.Message(Now, key));

        // Act
        var action = () => context.SaveChangesAsync(cancellationToken);

        // Assert
        var exception = await Assert.ThrowsAsync<DbUpdateException>(action);
        var postgres = exception.InnerException as PostgresException
            ?? throw new InvalidOperationException("Expected PostgreSQL error.");
        Assert.Equal(
            (PostgresErrorCodes.UniqueViolation, "unique_outbox_idempotency_key"),
            (postgres.SqlState, postgres.ConstraintName));
    }

    [Fact]
    public async Task ClaimPendingAsync_WhenFutureMessageExists_ClaimsOnlyDueMessages()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var due = IntegrationTestData.Message(Now.AddMinutes(-1));
        var future = IntegrationTestData.Message(Now.AddMinutes(10));
        await SeedAsync(cancellationToken, due, future);
        await using var context = _fixture.CreateAdministrativeContext();
        var repository = new OutboxRepository(context, new TestClock(Now));

        // Act
        var claimed = await repository.ClaimPendingAsync(
            TimeSpan.FromMinutes(5), 10, cancellationToken);

        // Assert
        Assert.Equal(due.Id, Assert.Single(claimed).Id);
    }

    [Fact]
    public async Task ClaimPendingAsync_WhenBatchIsLimited_RespectsBatchSize()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedAsync(
            cancellationToken,
            IntegrationTestData.Message(Now.AddMinutes(-3)),
            IntegrationTestData.Message(Now.AddMinutes(-2)),
            IntegrationTestData.Message(Now.AddMinutes(-1)));
        await using var context = _fixture.CreateAdministrativeContext();
        var repository = new OutboxRepository(context, new TestClock(Now));

        // Act
        var claimed = await repository.ClaimPendingAsync(
            TimeSpan.FromMinutes(5), 2, cancellationToken);

        // Assert
        Assert.Equal(2, claimed.Count);
    }

    [Fact]
    public async Task TryRenewLeaseAsync_WhenOwnerAndLeaseAreValid_ExtendsLease()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var message = IntegrationTestData.Message(Now);
        await SeedAsync(cancellationToken, message);
        var clock = new TestClock(Now);

        await using var context = _fixture.CreateAdministrativeContext();
        var repository = new OutboxRepository(context, clock);
        var claimed = Assert.Single(await repository.ClaimPendingAsync(
            TimeSpan.FromMinutes(5), 1, cancellationToken));
        clock.Advance(TimeSpan.FromMinutes(1));

        // Act
        var renewed = await repository.TryRenewLeaseAsync(
            claimed.Id, claimed.LeaseOwnerId!.Value, TimeSpan.FromMinutes(10), cancellationToken);
        context.ChangeTracker.Clear();
        var stored = await context.OutboxMessages
            .SingleAsync(value => value.Id == message.Id, cancellationToken);

        // Assert
        Assert.Equal(
            (true, (DateTime?)Now.AddMinutes(11)),
            (renewed, stored.LeaseExpiresAt));
    }

    [Fact]
    public async Task TryRenewLeaseAsync_WhenLeaseIsExpired_ReturnsFalse()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var message = IntegrationTestData.Message(Now);
        await SeedAsync(cancellationToken, message);
        var clock = new TestClock(Now);

        await using var context = _fixture.CreateAdministrativeContext();
        var repository = new OutboxRepository(context, clock);
        var claimed = Assert.Single(await repository.ClaimPendingAsync(
            TimeSpan.FromMinutes(5), 1, cancellationToken));
        clock.Advance(TimeSpan.FromMinutes(6));

        // Act
        var renewed = await repository.TryRenewLeaseAsync(
            claimed.Id,
            claimed.LeaseOwnerId!.Value,
            TimeSpan.FromMinutes(5),
            cancellationToken);

        // Assert
        Assert.False(renewed);
    }

    [Fact]
    public async Task TryRecordFailureAsync_WhenLeaseIsExpired_ReturnsFalse()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var message = IntegrationTestData.Message(Now);
        await SeedAsync(cancellationToken, message);
        var clock = new TestClock(Now);

        await using var context = _fixture.CreateAdministrativeContext();
        var repository = new OutboxRepository(context, clock);
        var claimed = Assert.Single(await repository.ClaimPendingAsync(
            TimeSpan.FromMinutes(5), 1, cancellationToken));
        clock.Advance(TimeSpan.FromMinutes(6));

        // Act
        var recorded = await repository.TryRecordFailureAsync(
            claimed.Id,
            claimed.LeaseOwnerId!.Value,
            "expired",
            Now.AddMinutes(10),
            cancellationToken);

        // Assert
        Assert.False(recorded);
    }

    [Fact]
    public async Task TryRenewLeaseAsync_WhenOwnerDiffers_ReturnsFalse()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var message = IntegrationTestData.Message(Now);
        await SeedAsync(cancellationToken, message);
        await using var context = _fixture.CreateAdministrativeContext();
        var repository = new OutboxRepository(context, new TestClock(Now));
        var claimed = Assert.Single(await repository.ClaimPendingAsync(
            TimeSpan.FromMinutes(5), 1, cancellationToken));

        // Act
        var renewed = await repository.TryRenewLeaseAsync(
            claimed.Id, Guid.NewGuid(), TimeSpan.FromMinutes(5), cancellationToken);

        // Assert
        Assert.False(renewed);
    }

    [Fact]
    public async Task ClaimPendingAsync_WhenRetryIsScheduled_ClaimsOnlyAfterNextAttemptAt()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var message = IntegrationTestData.Message(Now);
        await SeedAsync(cancellationToken, message);
        var clock = new TestClock(Now);
        await using var context = _fixture.CreateAdministrativeContext();
        var repository = new OutboxRepository(context, clock);
        var claimed = Assert.Single(await repository.ClaimPendingAsync(
            TimeSpan.FromMinutes(5), 1, cancellationToken));
        var nextAttemptAt = Now.AddMinutes(10);
        await repository.TryRecordFailureAsync(
            claimed.Id,
            claimed.LeaseOwnerId!.Value,
            "temporary",
            nextAttemptAt,
            cancellationToken);

        // Act
        var beforeSchedule = await repository.ClaimPendingAsync(
            TimeSpan.FromMinutes(5), 1, cancellationToken);
        clock.Set(nextAttemptAt);
        var atSchedule = await repository.ClaimPendingAsync(
            TimeSpan.FromMinutes(5), 1, cancellationToken);

        // Assert
        Assert.Equal(
            (0, message.Id),
            (beforeSchedule.Count, Assert.Single(atSchedule).Id));
    }

    private async Task SeedAsync(
        CancellationToken cancellationToken,
        params Domain.Entities.Jobs.OutboxMessage[] messages)
    {
        await using var context = _fixture.CreateAdministrativeContext();
        context.OutboxMessages.AddRange(messages);
        await context.SaveChangesAsync(cancellationToken);
    }
}
