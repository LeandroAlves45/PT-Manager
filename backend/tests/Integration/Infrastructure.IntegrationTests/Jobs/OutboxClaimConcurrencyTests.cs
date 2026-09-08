using Domain.Entities.Jobs;
using Domain.ValueObjects;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence;
using Npgsql;

namespace Infrastructure.IntegrationTests.Jobs;

/// <summary>
/// Verifica o claim da outbox sob concorrência real.
/// A outbox tem a mesma exigência dos durable jobs: uma mensagem representa um
/// efeito externo, por isso entregá-la a dois workers ao mesmo tempo produziria
/// o efeito duas vezes.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class OutboxClaimConcurrencyTests
{
    private static readonly DateTime Now = new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(1);

    private readonly PostgresContainerFixture _fixture;

    public OutboxClaimConcurrencyTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Claim_WhenMessageIsPending_AssignsLease()
    {
        var seed = await SeedTenantAsync();
        var messageId = await SeedMessageAsync(seed.TrainerId);

        var claimed = await ClaimAsync(seed.TrainerId, batchSize: 50);

        var message = Assert.Single(claimed, item => item.Id == messageId);
        Assert.Equal(JobStatus.Processing, message.Status);
        Assert.NotNull(message.LeaseOwnerId);
    }

    [Fact]
    public async Task Claim_ConcurrentDispatchers_ReceiveDisjointMessages()
    {
        var seed = await SeedTenantAsync();
        var messageIds = new List<Guid>();
        for (var index = 0; index < 12; index++)
            messageIds.Add(await SeedMessageAsync(seed.TrainerId));

        var batches = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => ClaimAsync(seed.TrainerId, batchSize: 12)));

        var claimedIds = batches
            .SelectMany(batch => batch.Select(message => message.Id))
            .Where(messageIds.Contains)
            .ToArray();

        Assert.Equal(claimedIds.Length, claimedIds.Distinct().Count());

        foreach (var messageId in messageIds)
            Assert.Equal(1, await CountOwnersAsync(messageId));
    }

    [Fact]
    public async Task Claim_WhenLeaseIsStillValid_AnotherWorkerCannotClaim()
    {
        var seed = await SeedTenantAsync();
        var messageId = await SeedMessageAsync(seed.TrainerId);

        await ClaimAsync(seed.TrainerId, batchSize: 50);
        var second = await ClaimAsync(seed.TrainerId, batchSize: 50);

        Assert.DoesNotContain(second, message => message.Id == messageId);
    }

    [Fact]
    public async Task Claim_WhenLeaseExpired_AnotherWorkerRecoversWithNewOwner()
    {
        var seed = await SeedTenantAsync();
        var messageId = await SeedMessageAsync(seed.TrainerId);

        var first = await ClaimAsync(seed.TrainerId, batchSize: 50);
        var originalOwner = first.Single(message => message.Id == messageId).LeaseOwnerId;

        var afterExpiry = Now.Add(Lease).AddMinutes(1);
        await ClaimAsync(seed.TrainerId, batchSize: 100, now: afterExpiry);

        var recoveredOwner = await ReadOwnerAsync(messageId);
        Assert.NotNull(recoveredOwner);
        Assert.NotEqual(originalOwner, recoveredOwner);
    }

    [Fact]
    public async Task Complete_OnlyTheCurrentOwnerSucceeds()
    {
        var seed = await SeedTenantAsync();
        var messageId = await SeedMessageAsync(seed.TrainerId);
        var claimed = await ClaimAsync(seed.TrainerId, batchSize: 50);
        var owner = claimed.Single(message => message.Id == messageId).LeaseOwnerId!.Value;

        Assert.False(await CompleteAsync(seed.TrainerId, messageId, Guid.NewGuid()));
        Assert.Equal("processing", await ReadStatusAsync(messageId));

        Assert.True(await CompleteAsync(seed.TrainerId, messageId, owner));
        Assert.Equal("completed", await ReadStatusAsync(messageId));
    }

    [Fact]
    public async Task RecordFailure_WithoutNextAttempt_MovesMessageToDeadLetter()
    {
        var seed = await SeedTenantAsync();
        var messageId = await SeedMessageAsync(seed.TrainerId);
        var claimed = await ClaimAsync(seed.TrainerId, batchSize: 50);
        var owner = claimed.Single(message => message.Id == messageId).LeaseOwnerId!.Value;

        await using var context = _fixture.CreateContext(seed.TrainerId);
        var repository = new OutboxRepository(context, new TestClock(Now));

        var applied = await repository.TryRecordFailureAsync(
            messageId,
            owner,
            "outbox_handler_not_registered",
            nextAttemptAt: null,
            TestContext.Current.CancellationToken);

        Assert.True(applied);
        Assert.Equal("dead_letter", await ReadStatusAsync(messageId));
    }

    private async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(
        Guid trainerId,
        int batchSize,
        DateTime? now = null)
    {
        await using var context = _fixture.CreateContext(trainerId);
        var repository = new OutboxRepository(context, new TestClock(now ?? Now));

        return await repository.ClaimPendingAsync(
            Lease, batchSize, TestContext.Current.CancellationToken);
    }

    private async Task<bool> CompleteAsync(Guid trainerId, Guid messageId, Guid owner)
    {
        await using var context = _fixture.CreateContext(trainerId);
        var repository = new OutboxRepository(context, new TestClock(Now));

        return await repository.TryCompleteAsync(
            messageId, owner, TestContext.Current.CancellationToken);
    }

    private async Task<Guid> SeedMessageAsync(Guid trainerId)
    {
        // O tipo é fictício de propósito: a Fase 5A não regista handlers de outbox.
        var message = new OutboxMessage(
            trainerId,
            "integration_test_message",
            "{\"value\":1}",
            $"idem-{Guid.NewGuid():N}",
            Guid.NewGuid(),
            Now);

        await using var context = _fixture.CreateContext(trainerId);
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return message.Id;
    }

    private Task<PostgresContainerFixture.TestTenantSeed> SeedTenantAsync() =>
        _fixture.SeedTenantWithClientAsync(
            $"outboxclaim-{Guid.NewGuid():N}",
            TestContext.Current.CancellationToken);

    private async Task<Guid?> ReadOwnerAsync(Guid messageId)
    {
        var owner = await _fixture.QueryScalarAsync<Guid>(
            "SELECT lease_owner_id FROM outbox_messages WHERE id = @id",
            TestContext.Current.CancellationToken,
            new NpgsqlParameter("id", messageId));

        return owner == Guid.Empty ? null : owner;
    }

    private Task<string?> ReadStatusAsync(Guid messageId) =>
        _fixture.QueryScalarAsync<string>(
            "SELECT status FROM outbox_messages WHERE id = @id",
            TestContext.Current.CancellationToken,
            new NpgsqlParameter("id", messageId));

    private Task<long> CountOwnersAsync(Guid messageId) =>
        _fixture.QueryScalarAsync<long>(
            """
            SELECT COUNT(DISTINCT lease_owner_id)
            FROM outbox_messages
            WHERE id = @id AND lease_owner_id IS NOT NULL
            """,
            TestContext.Current.CancellationToken,
            new NpgsqlParameter("id", messageId));
}
