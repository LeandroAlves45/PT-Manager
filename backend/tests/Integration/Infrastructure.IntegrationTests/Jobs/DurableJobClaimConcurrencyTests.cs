using Domain.Entities.Jobs;
using Domain.ValueObjects;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence;
using Npgsql;

namespace Infrastructure.IntegrationTests.Jobs;

/// <summary>
/// Verifica o claim de durable jobs sob concorrência real.
/// A exclusão mútua vem de `FOR UPDATE SKIP LOCKED` no PostgreSQL, não de
/// guards no Domain. Substituir a base de dados por InMemory removeria
/// exactamente o mecanismo que se pretende provar: dois workers a reclamar a
/// mesma fila ao mesmo tempo têm de receber conjuntos disjuntos.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DurableJobClaimConcurrencyTests
{
    private static readonly DateTime Now = new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(1);

    private readonly PostgresContainerFixture _fixture;

    public DurableJobClaimConcurrencyTests(PostgresContainerFixture fixture) =>
        _fixture = fixture;

    [Fact]
    public async Task Claim_WhenJobIsDue_AssignsLeaseAndIncrementsAttempts()
    {
        var seed = await SeedTenantAsync();
        var jobId = await SeedJobAsync(seed.TrainerId, scheduledAt: Now.AddMinutes(-1));

        var claimed = await ClaimAsync(seed.TrainerId, batchSize: 10);

        var job = Assert.Single(claimed, item => item.Id == jobId);
        Assert.Equal(JobStatus.Processing, job.Status);
        Assert.NotNull(job.LeaseOwnerId);
        Assert.Equal(1, job.Attempts);
    }

    [Fact]
    public async Task Claim_WhenJobIsScheduledInTheFuture_DoesNotClaimIt()
    {
        var seed = await SeedTenantAsync();
        var jobId = await SeedJobAsync(seed.TrainerId, scheduledAt: Now.AddHours(1));

        var claimed = await ClaimAsync(seed.TrainerId, batchSize: 10);

        Assert.DoesNotContain(claimed, job => job.Id == jobId);
    }

    [Fact]
    public async Task Claim_ConcurrentDispatchers_ReceiveDisjointJobs()
    {
        var seed = await SeedTenantAsync();
        var jobIds = new List<Guid>();
        for (var index = 0; index < 12; index++)
            jobIds.Add(await SeedJobAsync(seed.TrainerId, scheduledAt: Now.AddMinutes(-1)));

        // Quatro dispatchers concorrentes sobre a mesma fila.
        var batches = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => ClaimAsync(seed.TrainerId, batchSize: 12)));

        var claimedIds = batches
            .SelectMany(batch => batch.Select(job => job.Id))
            .Where(jobIds.Contains)
            .ToArray();

        // Nenhum job pode ser entregue a dois workers: o total reclamado tem de
        // ser exactamente igual ao número de IDs distintos reclamados.
        Assert.Equal(claimedIds.Length, claimedIds.Distinct().Count());

        // E cada job reclamado tem um único owner na base de dados.
        foreach (var jobId in jobIds)
            Assert.Equal(1, await CountOwnersAsync(jobId));
    }

    [Fact]
    public async Task Claim_WhenLeaseIsStillValid_AnotherWorkerCannotClaim()
    {
        var seed = await SeedTenantAsync();
        var jobId = await SeedJobAsync(seed.TrainerId, scheduledAt: Now.AddMinutes(-1));

        var first = await ClaimAsync(seed.TrainerId, batchSize: 10);
        Assert.Contains(first, job => job.Id == jobId);

        // Segundo worker no mesmo instante: o lease ainda é válido.
        var second = await ClaimAsync(seed.TrainerId, batchSize: 10);

        Assert.DoesNotContain(second, job => job.Id == jobId);
    }

    [Fact]
    public async Task Claim_WhenLeaseExpired_AnotherWorkerRecoversWithNewOwner()
    {
        var seed = await SeedTenantAsync();
        var jobId = await SeedJobAsync(seed.TrainerId, scheduledAt: Now.AddMinutes(-1));

        var first = await ClaimAsync(seed.TrainerId, batchSize: 10);
        var originalOwner = first.Single(job => job.Id == jobId).LeaseOwnerId;

        // O worker morreu: o lease expira sem conclusão. Outros testes partilham a
        // tabela, por isso a recuperação é confirmada pelo estado persistido deste
        // job e não pela composição do batch devolvido.
        var afterExpiry = Now.Add(Lease).AddMinutes(1);
        await ClaimAsync(seed.TrainerId, batchSize: 100, now: afterExpiry);

        var (recoveredOwner, attempts) = await ReadLeaseStateAsync(jobId);
        Assert.NotNull(recoveredOwner);
        Assert.NotEqual(originalOwner, recoveredOwner);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task RenewLease_OnlyTheCurrentOwnerSucceeds()
    {
        var seed = await SeedTenantAsync();
        var jobId = await SeedJobAsync(seed.TrainerId, scheduledAt: Now.AddMinutes(-1));
        var claimed = await ClaimAsync(seed.TrainerId, batchSize: 10);
        var owner = claimed.Single(job => job.Id == jobId).LeaseOwnerId!.Value;

        Assert.True(await RenewAsync(seed.TrainerId, jobId, owner));
        Assert.False(await RenewAsync(seed.TrainerId, jobId, Guid.NewGuid()));
    }

    [Fact]
    public async Task RenewLease_AfterLeaseExpired_Fails()
    {
        var seed = await SeedTenantAsync();
        var jobId = await SeedJobAsync(seed.TrainerId, scheduledAt: Now.AddMinutes(-1));
        var claimed = await ClaimAsync(seed.TrainerId, batchSize: 10);
        var owner = claimed.Single(job => job.Id == jobId).LeaseOwnerId!.Value;

        var afterExpiry = Now.Add(Lease).AddMinutes(1);

        // Renovar um lease já expirado permitiria dois donos em simultâneo.
        Assert.False(await RenewAsync(seed.TrainerId, jobId, owner, now: afterExpiry));
    }

    [Fact]
    public async Task Complete_OnlyTheCurrentOwnerSucceeds()
    {
        var seed = await SeedTenantAsync();
        var jobId = await SeedJobAsync(seed.TrainerId, scheduledAt: Now.AddMinutes(-1));
        var claimed = await ClaimAsync(seed.TrainerId, batchSize: 10);
        var owner = claimed.Single(job => job.Id == jobId).LeaseOwnerId!.Value;

        Assert.False(await CompleteAsync(seed.TrainerId, jobId, Guid.NewGuid()));
        Assert.Equal("processing", await ReadStatusAsync(jobId));

        Assert.True(await CompleteAsync(seed.TrainerId, jobId, owner));
        Assert.Equal("completed", await ReadStatusAsync(jobId));
    }

    [Fact]
    public async Task RecordFailure_WithNextAttempt_ReturnsJobToPendingQueue()
    {
        var seed = await SeedTenantAsync();
        var jobId = await SeedJobAsync(seed.TrainerId, scheduledAt: Now.AddMinutes(-1));
        var claimed = await ClaimAsync(seed.TrainerId, batchSize: 10);
        var owner = claimed.Single(job => job.Id == jobId).LeaseOwnerId!.Value;

        var applied = await RecordFailureAsync(
            seed.TrainerId, jobId, owner, "resend_http_503", Now.AddMinutes(5));

        Assert.True(applied);
        Assert.Equal("pending", await ReadStatusAsync(jobId));
    }

    [Fact]
    public async Task RecordFailure_WithoutNextAttempt_MovesJobToDeadLetter()
    {
        var seed = await SeedTenantAsync();
        var jobId = await SeedJobAsync(seed.TrainerId, scheduledAt: Now.AddMinutes(-1));
        var claimed = await ClaimAsync(seed.TrainerId, batchSize: 10);
        var owner = claimed.Single(job => job.Id == jobId).LeaseOwnerId!.Value;

        var applied = await RecordFailureAsync(
            seed.TrainerId, jobId, owner, "notification_payload_invalid", nextAttemptAt: null);

        Assert.True(applied);
        Assert.Equal("dead_letter", await ReadStatusAsync(jobId));
    }

    [Fact]
    public async Task RecordFailure_ByStaleOwner_DoesNotChangeTheJob()
    {
        var seed = await SeedTenantAsync();
        var jobId = await SeedJobAsync(seed.TrainerId, scheduledAt: Now.AddMinutes(-1));
        await ClaimAsync(seed.TrainerId, batchSize: 10);

        var applied = await RecordFailureAsync(
            seed.TrainerId, jobId, Guid.NewGuid(), "resend_http_503", Now.AddMinutes(5));

        Assert.False(applied);
        Assert.Equal("processing", await ReadStatusAsync(jobId));
    }

    private async Task<IReadOnlyList<DurableJob>> ClaimAsync(
        Guid trainerId,
        int batchSize,
        DateTime? now = null)
    {
        await using var context = _fixture.CreateContext(trainerId);
        var repository = new DurableJobRepository(context, new TestClock(now ?? Now));

        return await repository.ClaimDueJobsAsync(
            Lease, batchSize, TestContext.Current.CancellationToken);
    }

    private async Task<bool> RenewAsync(
        Guid trainerId,
        Guid jobId,
        Guid owner,
        DateTime? now = null)
    {
        await using var context = _fixture.CreateContext(trainerId);
        var repository = new DurableJobRepository(context, new TestClock(now ?? Now));

        return await repository.TryRenewLeaseAsync(
            jobId, owner, Lease, TestContext.Current.CancellationToken);
    }

    private async Task<bool> CompleteAsync(Guid trainerId, Guid jobId, Guid owner)
    {
        await using var context = _fixture.CreateContext(trainerId);
        var repository = new DurableJobRepository(context, new TestClock(Now));

        return await repository.TryCompleteAsync(
            jobId, owner, TestContext.Current.CancellationToken);
    }

    private async Task<bool> RecordFailureAsync(
        Guid trainerId,
        Guid jobId,
        Guid owner,
        string failureCode,
        DateTime? nextAttemptAt)
    {
        await using var context = _fixture.CreateContext(trainerId);
        var repository = new DurableJobRepository(context, new TestClock(Now));

        return await repository.TryRecordFailureAsync(
            jobId, owner, failureCode, nextAttemptAt, TestContext.Current.CancellationToken);
    }

    private async Task<Guid> SeedJobAsync(Guid trainerId, DateTime scheduledAt)
    {
        var job = new DurableJob(
            trainerId,
            "send_notification",
            1,
            $"{{\"notification_id\":\"{Guid.NewGuid()}\"}}",
            $"idem-{Guid.NewGuid():N}",
            Guid.NewGuid(),
            scheduledAt,
            Now);

        await using var context = _fixture.CreateContext(trainerId);
        context.DurableJobs.Add(job);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return job.Id;
    }

    private Task<PostgresContainerFixture.TestTenantSeed> SeedTenantAsync() =>
        _fixture.SeedTenantWithClientAsync(
            $"jobclaim-{Guid.NewGuid():N}",
            TestContext.Current.CancellationToken);

    private async Task<(Guid? LeaseOwnerId, int Attempts)> ReadLeaseStateAsync(Guid jobId)
    {
        var owner = await _fixture.QueryScalarAsync<Guid>(
            "SELECT lease_owner_id FROM durable_jobs WHERE id = @id",
            TestContext.Current.CancellationToken,
            new NpgsqlParameter("id", jobId));

        var attempts = await _fixture.QueryScalarAsync<int>(
            "SELECT attempts FROM durable_jobs WHERE id = @id",
            TestContext.Current.CancellationToken,
            new NpgsqlParameter("id", jobId));

        return (owner == Guid.Empty ? null : owner, attempts);
    }

    private Task<string?> ReadStatusAsync(Guid jobId) =>
        _fixture.QueryScalarAsync<string>(
            "SELECT status FROM durable_jobs WHERE id = @id",
            TestContext.Current.CancellationToken,
            new NpgsqlParameter("id", jobId));

    private Task<long> CountOwnersAsync(Guid jobId) =>
        _fixture.QueryScalarAsync<long>(
            """
            SELECT COUNT(DISTINCT lease_owner_id)
            FROM durable_jobs
            WHERE id = @id AND lease_owner_id IS NOT NULL
            """,
            TestContext.Current.CancellationToken,
            new NpgsqlParameter("id", jobId));
}
