using System.Text.Json;
using Application.Features.Notifications.Abstractions;
using Infrastructure.IntegrationTests.Clients;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Errors;
using Infrastructure.Persistence.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Notifications;

/// <summary>Verifica a atomicidade, idempotência e isolamento de tenant do enqueue de notificações.</summary>
[Collection(PostgresCollection.Name)]
public sealed class NotificationQueueStoreTests
{
    private readonly PostgresContainerFixture _fixture;
    private readonly ClientStoreTestContextFactory _contextFactory;

    public NotificationQueueStoreTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
        _contextFactory = new ClientStoreTestContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task Enqueue_PersistsNotificationAndJobAtomically()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await _fixture.SeedTenantWithClientAsync("atomic", cancellationToken);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = CreateStore(context, seed.TrainerId);

        var result = await store.EnqueueAsync(
            Request(seed.TrainerId, seed.ClientId, "atomic-op"),
            cancellationToken);

        Assert.Equal(NotificationQueueStoreStatus.Queued, result.Kind);

        context.ChangeTracker.Clear();
        var notification = await context.Notifications
            .AsNoTracking()
            .SingleAsync(item => item.Id == result.NotificationId, cancellationToken);
        var job = await context.DurableJobs
            .AsNoTracking()
            .SingleAsync(
                item => item.IdempotencyKey.Contains("atomic"),
                cancellationToken);

        using var payload = JsonDocument.Parse(job.Payload);
        Assert.Equal(
            notification.Id.ToString(),
            payload.RootElement.GetProperty("notification_id").GetString());
        Assert.DoesNotContain("recipient@example.test", job.Payload);
        Assert.DoesNotContain("token", job.Payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Enqueue_SameOperationTwice_ReturnsOriginalWithoutDuplicateRows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await _fixture.SeedTenantWithClientAsync("retry", cancellationToken);
        var request = Request(seed.TrainerId, seed.ClientId, "retry-op");

        await using var firstContext = _fixture.CreateContext(seed.TrainerId);
        var first = await CreateStore(firstContext, seed.TrainerId)
            .EnqueueAsync(request, cancellationToken);

        await using var secondContext = _fixture.CreateContext(seed.TrainerId);
        var second = await CreateStore(secondContext, seed.TrainerId)
            .EnqueueAsync(request, cancellationToken);

        Assert.Equal(first.NotificationId, second.NotificationId);
        Assert.Equal(NotificationQueueStoreStatus.AlreadyQueued, second.Kind);

        await using var verification = _fixture.CreateContext(seed.TrainerId);
        Assert.Equal(
            1,
            await verification.Notifications
                .AsNoTracking()
                .CountAsync(item => item.Id == first.NotificationId, cancellationToken));
        Assert.Equal(
            1,
            await verification.DurableJobs
                .AsNoTracking()
                .CountAsync(
                    item => item.IdempotencyKey.Contains("retry-op"),
                    cancellationToken));
    }

    [Fact]
    public async Task Enqueue_ClientFromOtherTenant_ReturnsNotFoundAndPersistsNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var owner = await _fixture.SeedTenantWithClientAsync("owner", cancellationToken);
        var requester = await _fixture.SeedTenantWithClientAsync("requester", cancellationToken);
        await using var context = _fixture.CreateContext(requester.TrainerId);
        var store = CreateStore(context, requester.TrainerId);

        var result = await store.EnqueueAsync(
            Request(requester.TrainerId, owner.ClientId, "cross-tenant-op"),
            cancellationToken);

        Assert.Equal(NotificationQueueStoreStatus.ClientNotFound, result.Kind);

        await using var verification = _fixture.CreateContext(requester.TrainerId);
        Assert.Equal(
            0,
            await verification.DurableJobs
                .AsNoTracking()
                .CountAsync(
                    item => item.IdempotencyKey.Contains("cross-tenant-op"),
                    cancellationToken));
    }

    [Fact]
    public async Task Enqueue_ConcurrentSameOperationKey_ProducesSingleNotificationAndJob()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await _fixture.SeedTenantWithClientAsync("concurrent", cancellationToken);
        var request = Request(seed.TrainerId, seed.ClientId, "concurrent-op");

        var first = EnqueueWithOwnContextAsync(seed.TrainerId, request, cancellationToken);
        var second = EnqueueWithOwnContextAsync(seed.TrainerId, request, cancellationToken);
        var results = await Task.WhenAll(first, second);

        Assert.Equal(results[0].NotificationId, results[1].NotificationId);
        Assert.Contains(NotificationQueueStoreStatus.Queued, results.Select(item => item.Kind));
        Assert.Contains(NotificationQueueStoreStatus.AlreadyQueued, results.Select(item => item.Kind));

        await using var verification = _fixture.CreateContext(seed.TrainerId);
        Assert.Equal(
            1,
            await verification.Notifications
                .AsNoTracking()
                .CountAsync(item => item.Id == results[0].NotificationId, cancellationToken));
        Assert.Equal(
            1,
            await verification.DurableJobs
                .AsNoTracking()
                .CountAsync(
                    item => item.IdempotencyKey.Contains("concurrent-op"),
                    cancellationToken));
    }

    [Fact]
    public async Task Enqueue_CancelledToken_PersistsNothing()
    {
        var seed = await _fixture.SeedTenantWithClientAsync(
            "cancelled",
            TestContext.Current.CancellationToken);
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = CreateStore(context, seed.TrainerId);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.EnqueueAsync(
            Request(seed.TrainerId, seed.ClientId, "cancelled-op"),
            cancellationSource.Token));

        await using var verification = _fixture.CreateContext(seed.TrainerId);
        Assert.Equal(
            0,
            await verification.DurableJobs
                .AsNoTracking()
                .CountAsync(
                    item => item.IdempotencyKey.Contains("cancelled-op"),
                    TestContext.Current.CancellationToken));
    }

    private async Task<NotificationQueueStoreResult> EnqueueWithOwnContextAsync(
        Guid trainerId,
        NotificationQueueRequest request,
        CancellationToken cancellationToken)
    {
        await using var context = _contextFactory.Create(trainerId);
        return await CreateStore(context, trainerId).EnqueueAsync(request, cancellationToken);
    }

    private static NotificationQueueStore CreateStore(
        Infrastructure.Data.PtManagerDbContext context,
        Guid trainerId)
    {
        return new NotificationQueueStore(
            context,
            TestTenantContext.ForTrainer(trainerId),
            new PostgresConstraintTranslator());
    }

    private static NotificationQueueRequest Request(
        Guid trainerId,
        Guid? clientId,
        string operationKey)
    {
        var now = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);
        return new NotificationQueueRequest(
            trainerId,
            clientId,
            "recipient@example.test",
            "account",
            "email_confirmation",
            "{\"user_id\":\"11111111-1111-1111-1111-111111111111\"}",
            operationKey,
            Guid.NewGuid(),
            now,
            now);
    }
}
