using Domain.ValueObjects;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.IntegrationTests.Jobs;

[Collection(PostgresCollection.Name)]
public sealed class DurableJobRepositoryTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 8, 5, 13, 0, 0, DateTimeKind.Utc);

    private readonly PostgresContainerFixture _fixture;

    public DurableJobRepositoryTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Claims não recebem ID de cenário. Limpar a tabela evita que um teste
        // reclame jobs pendentes criados por teste da mesma classe.
        await _fixture.ExecuteSqlAsync(
            "TRUNCATE TABLE durable_jobs;",
            TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ClaimDueJobsAsync_WhenFutureJobExists_ClaimsOnlyDueJob()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var due = IntegrationTestData.Job(Now.AddMinutes(-1), Now);
        var future = IntegrationTestData.Job(Now.AddMinutes(10), Now);
        await SeedAsync(cancellationToken, due, future);

        await using var context = _fixture.CreateAdministrativeContext();
        var reposisotory = new DurableJobRepository(context, new TestClock(Now));

        // Act
        var claimed = await reposisotory.ClaimDueJobsAsync(
            TimeSpan.FromMinutes(5),
            10,
            cancellationToken);

        // Assert
        Assert.Equal(due.Id, Assert.Single(claimed).Id);
    }

    [Fact]
    public async Task ClaimDueJobsAsync_WhenBatchIsLimited_RespectsBatchSize()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedAsync(
            cancellationToken,
            IntegrationTestData.Job(Now.AddMinutes(-3), Now),
            IntegrationTestData.Job(Now.AddMinutes(-2), Now),
            IntegrationTestData.Job(Now.AddMinutes(-1), Now));

        await using var context = _fixture.CreateAdministrativeContext();
        var reposisotory = new DurableJobRepository(context, new TestClock(Now));

        // Act
        var claimed = await reposisotory.ClaimDueJobsAsync(
            TimeSpan.FromMinutes(5),
            2,
            cancellationToken);

        // Assert
        Assert.Equal(2, claimed.Count);
    }

    [Fact]
    public async Task ClaimDueJobsAsync_WhenTwoWorkersRunConcurrently_ReturnsDisjointSets()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedAsync(
            cancellationToken,
            IntegrationTestData.Job(Now.AddMinutes(-4), Now),
            IntegrationTestData.Job(Now.AddMinutes(-3), Now),
            IntegrationTestData.Job(Now.AddMinutes(-2), Now),
            IntegrationTestData.Job(Now.AddMinutes(-1), Now));

        await using var contextA = _fixture.CreateAdministrativeContext();
        await using var contextB = _fixture.CreateAdministrativeContext();
        var workerA = new DurableJobRepository(contextA, new TestClock(Now));
        var workerB = new DurableJobRepository(contextB, new TestClock(Now));

        // Act
        var results = await Task.WhenAll(
            workerA.ClaimDueJobsAsync(TimeSpan.FromMinutes(5), 2, cancellationToken),
            workerB.ClaimDueJobsAsync(TimeSpan.FromMinutes(5), 2, cancellationToken));
        var overlapCount = results[0].Select(job => job.Id)
            .Intersect(results[1].Select(job => job.Id))
            .Count();

        // Assert
        Assert.Equal((4, 0), (results.Sum(set => set.Count), overlapCount));
    }

    [Fact]
    public async Task ClaimDueJobsAsync_WhenLeaseExpired_ReclaimsAbandonedJob()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var job = IntegrationTestData.Job(Now.AddMinutes(-1), Now);
        await SeedAsync(cancellationToken, job);
        var clock = new TestClock(Now);

        await using var firstContext = _fixture.CreateAdministrativeContext();
        var firstWorker = new DurableJobRepository(firstContext, clock);
        var firstClaim = Assert.Single(await firstWorker.ClaimDueJobsAsync(
            TimeSpan.FromMinutes(5), 1, cancellationToken));
        clock.Advance(TimeSpan.FromMinutes(6));

        await using var secondContext = _fixture.CreateAdministrativeContext();
        var secondWorker = new DurableJobRepository(secondContext, clock);

        // Act
        var secondClaim = Assert.Single(await secondWorker.ClaimDueJobsAsync(
            TimeSpan.FromMinutes(5), 1, cancellationToken));

        // Assert
        Assert.Equal(
            (job.Id, 2, false),
            (secondClaim.Id, secondClaim.Attempts, firstClaim.LeaseOwnerId == secondClaim.LeaseOwnerId));
    }

    [Fact]
    public async Task ClaimDueJobsAsync_WhenRetryAndFirstAttemptAreDue_OrdersByEffectiveDate()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var retry = IntegrationTestData.Job(Now.AddMinutes(-30), Now.AddMinutes(-30));
        var leaseOwnerId = Guid.NewGuid();
        retry.Claim(leaseOwnerId, TimeSpan.FromMinutes(5), Now.AddMinutes(-20));
        retry.RecordFailure(leaseOwnerId, "temporary", Now.AddMinutes(-19));
        retry.ScheduleRetry(Now.AddMinutes(-1), Now.AddMinutes(-19));
        var firstAttempt = IntegrationTestData.Job(Now.AddMinutes(-2), Now);
        await SeedAsync(cancellationToken, retry, firstAttempt);

        await using var context = _fixture.CreateAdministrativeContext();
        var reposisotory = new DurableJobRepository(context, new TestClock(Now));

        // Act
        var claimed = await reposisotory.ClaimDueJobsAsync(
            TimeSpan.FromMinutes(5),
            2,
            cancellationToken);

        // Assert
        Assert.Equal(new[] { firstAttempt.Id, retry.Id }, claimed.Select(job => job.Id));
    }

    [Fact]
    public async Task ClaimDueJobsAsync_WhenRetryIsScheduled_ClaimsOnlyAfterNextAttemptAt()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var job = IntegrationTestData.Job(Now.AddMinutes(-1), Now);
        await SeedAsync(cancellationToken, job);
        var clock = new TestClock(Now);

        await using var context = _fixture.CreateAdministrativeContext();
        var repository = new DurableJobRepository(context, clock);
        var claimed = Assert.Single(await repository.ClaimDueJobsAsync(
            TimeSpan.FromMinutes(5),
            1,
            cancellationToken));
        var nextAttemptAt = Now.AddMinutes(10);
        await repository.TryRecordFailureAsync(
            claimed.Id,
            claimed.LeaseOwnerId!.Value,
            "temporary",
            nextAttemptAt,
            cancellationToken);

        // Act
        var beforeScheduled = await repository.ClaimDueJobsAsync(
            TimeSpan.FromMinutes(5),
            1,
            cancellationToken);
        clock.Advance(TimeSpan.FromMinutes(10));
        var atScheduled = await repository.ClaimDueJobsAsync(
            TimeSpan.FromMinutes(5),
            1,
            cancellationToken);

        // Assert
        Assert.Equal((0, job.Id), (beforeScheduled.Count, Assert.Single(atScheduled).Id));
    }

    [Fact]
    public async Task TryCompleteAsync_WhenLeaseIsValid_CompletesAndClearsLease()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var job = IntegrationTestData.Job(Now.AddMinutes(-1), Now);
        await SeedAsync(cancellationToken, job);
        var clock = new TestClock(Now);

        await using var context = _fixture.CreateAdministrativeContext();
        var repository = new DurableJobRepository(context, clock);
        var claimed = Assert.Single(await repository.ClaimDueJobsAsync(
            TimeSpan.FromMinutes(5),
            1,
            cancellationToken));

        // Act
        var completed = await repository.TryCompleteAsync(
            claimed.Id, claimed.LeaseOwnerId!.Value, cancellationToken);
        context.ChangeTracker.Clear();
        var stored = await context.DurableJobs
            .SingleAsync(value => value.Id == job.Id, cancellationToken);

        // Assert
        Assert.Equal(
            (true, JobStatus.Completed, (Guid?)null, (DateTime?)null),
            (completed, stored.Status, stored.LeaseOwnerId, stored.LeaseExpiresAt));
    }

    [Fact]
    public async Task TryCompleteAsync_WhenLeaseExpired_ReturnsFalse()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var job = IntegrationTestData.Job(Now.AddMinutes(-1), Now);
        await SeedAsync(cancellationToken, job);
        var clock = new TestClock(Now);

        await using var context = _fixture.CreateAdministrativeContext();
        var repository = new DurableJobRepository(context, clock);
        var claimed = Assert.Single(await repository.ClaimDueJobsAsync(
            TimeSpan.FromMinutes(5),
            1,
            cancellationToken));
        clock.Advance(TimeSpan.FromMinutes(6));

        // Act
        var completed = await repository.TryCompleteAsync(
            claimed.Id, claimed.LeaseOwnerId!.Value, cancellationToken);

        // Assert
        Assert.False(completed);
    }

    [Fact]
    public async Task TryCompleteAsync_WhenOwnerDiffers_ReturnsFalse()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var job = IntegrationTestData.Job(Now.AddMinutes(-1), Now);
        await SeedAsync(cancellationToken, job);

        await using var context = _fixture.CreateAdministrativeContext();
        var repository = new DurableJobRepository(context, new TestClock(Now));
        var claimed = Assert.Single(await repository.ClaimDueJobsAsync(
            TimeSpan.FromMinutes(5), 1, cancellationToken));

        // Act
        var completed = await repository.TryCompleteAsync(
            claimed.Id, Guid.NewGuid(), cancellationToken);

        // Assert
        Assert.False(completed);
    }

    [Fact]
    public async Task TryRecordFailureAsync_WhenRetryIsProvided_ReturnsJobToPending()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var job = IntegrationTestData.Job(Now.AddMinutes(-1), Now);
        await SeedAsync(cancellationToken, job);
        var clock = new TestClock(Now);

        await using var context = _fixture.CreateAdministrativeContext();
        var repository = new DurableJobRepository(context, clock);
        var claimed = Assert.Single(await repository.ClaimDueJobsAsync(
            TimeSpan.FromMinutes(5),
            1,
            cancellationToken));
        var nextAttemptAt = Now.AddMinutes(10);

        // Act
        var recorded = await repository.TryRecordFailureAsync(
            claimed.Id,
            claimed.LeaseOwnerId!.Value,
            "temporary",
            nextAttemptAt,
            cancellationToken);
        context.ChangeTracker.Clear();
        var stored = await context.DurableJobs
            .SingleAsync(value => value.Id == job.Id, cancellationToken);

        // Assert
        Assert.Equal(
            (true, JobStatus.Pending, (DateTime?)nextAttemptAt),
            (recorded, stored.Status, stored.NextAttemptAt));
    }

    [Fact]
    public async Task TryRecordFailureAsync_WhenRetryIsNull_MovesJobToDeadLetter()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var job = IntegrationTestData.Job(Now.AddMinutes(-1), Now);
        await SeedAsync(cancellationToken, job);
        var clock = new TestClock(Now);

        await using var context = _fixture.CreateAdministrativeContext();
        var repository = new DurableJobRepository(context, clock);
        var claimed = Assert.Single(await repository.ClaimDueJobsAsync(
            TimeSpan.FromMinutes(5),
            1,
            cancellationToken));

        // Act
        var recorded = await repository.TryRecordFailureAsync(
            claimed.Id,
            claimed.LeaseOwnerId!.Value,
            "permanent",
            null,
            cancellationToken);
        context.ChangeTracker.Clear();
        var stored = await context.DurableJobs
            .SingleAsync(value => value.Id == job.Id, cancellationToken);

        // Assert
        Assert.Equal(
            (true, JobStatus.DeadLetter),
            (recorded, stored.Status));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TryRecordFailureAsync_WhenRetryIsNotFuture_ThrowsArgumentOutOfRangeException(
        int offsetMinutes)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var job = IntegrationTestData.Job(Now.AddMinutes(-1), Now);
        await SeedAsync(cancellationToken, job);

        await using var context = _fixture.CreateAdministrativeContext();
        var repository = new DurableJobRepository(context, new TestClock(Now));
        var claimed = Assert.Single(await repository.ClaimDueJobsAsync(
            TimeSpan.FromMinutes(5), 1, cancellationToken));

        // Act
        var action = () => repository.TryRecordFailureAsync(
            claimed.Id,
            claimed.LeaseOwnerId!.Value,
            "temporary",
            Now.AddMinutes(offsetMinutes),
            cancellationToken);

        // Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(action);
    }

    [Fact]
    public async Task TryRecordFailureAsync_WhenLeaseExpired_ReturnsFalse()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var job = IntegrationTestData.Job(Now.AddMinutes(-1), Now);
        await SeedAsync(cancellationToken, job);
        var clock = new TestClock(Now);

        await using var context = _fixture.CreateAdministrativeContext();
        var repository = new DurableJobRepository(context, clock);
        var claimed = Assert.Single(await repository.ClaimDueJobsAsync(
            TimeSpan.FromMinutes(5), 1, cancellationToken));
        clock.Advance(TimeSpan.FromMinutes(6));

        // Act
        var recorded = await repository.TryRecordFailureAsync(
            claimed.Id,
            claimed.LeaseOwnerId!.Value,
            "expired",
            clock.UtcNow.AddMinutes(10),
            cancellationToken);

        // Assert
        Assert.False(recorded);
    }

    [Fact]
    public async Task TryRecordFailureAsync_WhenOwnerDiffers_ReturnsFalse()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var job = IntegrationTestData.Job(Now.AddMinutes(-1), Now);
        await SeedAsync(cancellationToken, job);

        await using var context = _fixture.CreateAdministrativeContext();
        var repository = new DurableJobRepository(context, new TestClock(Now));
        var claimed = Assert.Single(await repository.ClaimDueJobsAsync(
            TimeSpan.FromMinutes(5), 1, cancellationToken));

        // Act
        var recorded = await repository.TryRecordFailureAsync(
            claimed.Id,
            Guid.NewGuid(),
            "wrong owner",
            Now.AddMinutes(10),
            cancellationToken);

        // Assert
        Assert.False(recorded);
    }

    [Fact]
    public async Task TryRenewLeaseAsync_WhenOwnerAndLeaseAreValid_ExtendsLease()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var job = IntegrationTestData.Job(Now.AddMinutes(-1), Now);
        await SeedAsync(cancellationToken, job);
        var clock = new TestClock(Now);

        await using var context = _fixture.CreateAdministrativeContext();
        var repository = new DurableJobRepository(context, clock);
        var claimed = Assert.Single(await repository.ClaimDueJobsAsync(
            TimeSpan.FromMinutes(5),
            1,
            cancellationToken));

        // Act
        var renewed = await repository.TryRenewLeaseAsync(
            claimed.Id, claimed.LeaseOwnerId!.Value, TimeSpan.FromMinutes(10), cancellationToken);
        context.ChangeTracker.Clear();
        var stored = await context.DurableJobs
            .SingleAsync(value => value.Id == job.Id, cancellationToken);

        // Assert
        Assert.Equal(
            (true, (DateTime?)Now.AddMinutes(10)),
            (renewed, stored.LeaseExpiresAt));
    }

    [Fact]
    public async Task TryRenewLeaseAsync_WhenLeaseExpired_ReturnsFalse()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var job = IntegrationTestData.Job(Now.AddMinutes(-1), Now);
        await SeedAsync(cancellationToken, job);
        var clock = new TestClock(Now);

        await using var context = _fixture.CreateAdministrativeContext();
        var repository = new DurableJobRepository(context, clock);
        var claimed = Assert.Single(await repository.ClaimDueJobsAsync(
            TimeSpan.FromMinutes(5),
            1,
            cancellationToken));
        clock.Advance(TimeSpan.FromMinutes(6));

        // Act
        var renewed = await repository.TryRenewLeaseAsync(
            claimed.Id, claimed.LeaseOwnerId!.Value, TimeSpan.FromMinutes(5), cancellationToken);

        // Assert
        Assert.False(renewed);
    }

    [Fact]
    public async Task TryRenewLeaseAsync_WhenOwnerDiffers_ReturnsFalse()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var job = IntegrationTestData.Job(Now.AddMinutes(-1), Now);
        await SeedAsync(cancellationToken, job);
        await using var context = _fixture.CreateAdministrativeContext();
        var repository = new DurableJobRepository(context, new TestClock(Now));
        var claimed = Assert.Single(await repository.ClaimDueJobsAsync(
            TimeSpan.FromMinutes(5),
            1,
            cancellationToken));

        // Act
        var renewed = await repository.TryRenewLeaseAsync(
            claimed.Id, Guid.NewGuid(), TimeSpan.FromMinutes(5), cancellationToken);

        // Assert
        Assert.False(renewed);
    }

    [Fact]
    public async Task SaveChanges_WhenIdempotencyKeyIsDuplicated_ThrowsDbUpdateException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var key = Guid.NewGuid().ToString("N");
        await using var context = _fixture.CreateAdministrativeContext();
        context.DurableJobs.AddRange(
            IntegrationTestData.Job(Now, Now, key),
            IntegrationTestData.Job(Now, Now, key));

        // Act
        var action = () => context.SaveChangesAsync(cancellationToken);

        // Assert
        var exception = await Assert.ThrowsAsync<DbUpdateException>(action);
        var postgres = exception.InnerException as PostgresException
            ?? throw new InvalidOperationException("Expected PostgresSQL error.");
        Assert.Equal(
            (PostgresErrorCodes.UniqueViolation, "unique_idempotency_key"),
            (postgres.SqlState, postgres.ConstraintName));
    }

    private async Task SeedAsync(
        CancellationToken cancellationToken,
        params Domain.Entities.Jobs.DurableJob[] jobs)
    {
        await using var context = _fixture.CreateAdministrativeContext();
        context.DurableJobs.AddRange(jobs);
        await context.SaveChangesAsync(cancellationToken);
    }
}
