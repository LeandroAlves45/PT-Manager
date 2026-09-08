using Application.Features.Notifications.Delivery;
using Domain.Entities.Jobs;
using Domain.Entities.Notifications;
using Domain.ValueObjects;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Notifications;
using Npgsql;

namespace Infrastructure.IntegrationTests.Notifications;

/// <summary>
/// Verifica o estado de entrega das notificações contra PostgreSQL real.
/// Todas as mutações exigem, na mesma instrução SQL, o lease válido do job.
/// É essa condição que impede um worker que já perdeu o lease de marcar como
/// enviada uma notificação que outro worker está a processar.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class NotificationDeliveryStoreTests
{
    private static readonly DateTime Now = new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);

    private readonly PostgresContainerFixture _fixture;

    public NotificationDeliveryStoreTests(PostgresContainerFixture fixture) =>
        _fixture = fixture;

    [Fact]
    public async Task Prepare_WhenPendingAndLeaseValid_ReturnsReadyMessage()
    {
        var scenario = await CreateScenarioAsync();

        var preparation = await PrepareAsync(scenario);

        Assert.Equal(NotificationDeliveryPreparationStatus.Ready, preparation.Status);
        Assert.NotNull(preparation.Message);
        Assert.Equal(scenario.NotificationId, preparation.Message.NotificationId);
        Assert.Equal("session_reminder", preparation.Message.TemplateKey);
    }

    [Fact]
    public async Task Prepare_WhenLeaseBelongsToAnotherOwner_ReturnsLeaseLost()
    {
        var scenario = await CreateScenarioAsync();

        var preparation = await PrepareAsync(scenario, leaseOwnerId: Guid.NewGuid());

        Assert.Equal(NotificationDeliveryPreparationStatus.LeaseLost, preparation.Status);
        Assert.Null(preparation.Message);
    }

    [Fact]
    public async Task Prepare_WhenLeaseExpired_ReturnsLeaseLost()
    {
        var scenario = await CreateScenarioAsync(leaseExpiresAt: Now.AddMinutes(-1));

        var preparation = await PrepareAsync(scenario);

        Assert.Equal(NotificationDeliveryPreparationStatus.LeaseLost, preparation.Status);
    }

    [Fact]
    public async Task Prepare_WhenNotificationAlreadySent_ReportsAlreadyDelivered()
    {
        var scenario = await CreateScenarioAsync();
        await SetNotificationStatusAsync(scenario.NotificationId, "sent");

        var preparation = await PrepareAsync(scenario);

        // Reenviar seria visível para o cliente final, por isso este estado é
        // tratado como sucesso e não como erro.
        Assert.Equal(
            NotificationDeliveryPreparationStatus.AlreadyDelivered,
            preparation.Status);
    }

    [Fact]
    public async Task Prepare_WhenNotificationMissing_ReturnsNotFound()
    {
        var scenario = await CreateScenarioAsync();
        var missing = scenario with { NotificationId = Guid.NewGuid() };

        var preparation = await PrepareAsync(missing);

        Assert.Equal(NotificationDeliveryPreparationStatus.NotFound, preparation.Status);
    }

    [Fact]
    public async Task Prepare_WhenPreviouslyFailed_RequeuesToPending()
    {
        var scenario = await CreateScenarioAsync();
        await SetNotificationStatusAsync(scenario.NotificationId, "failed");

        var preparation = await PrepareAsync(scenario);

        Assert.Equal(NotificationDeliveryPreparationStatus.Ready, preparation.Status);
        Assert.Equal("pending", await ReadStatusAsync(scenario.NotificationId));
    }

    [Fact]
    public async Task Prepare_WhenStatusIsTerminalBounced_ReturnsInvalidState()
    {
        var scenario = await CreateScenarioAsync();
        await SetNotificationStatusAsync(scenario.NotificationId, "bounced");

        var preparation = await PrepareAsync(scenario);

        Assert.Equal(NotificationDeliveryPreparationStatus.InvalidState, preparation.Status);
    }

    [Fact]
    public async Task MarkSent_WhenLeaseValid_AppliesAndStampsSentAt()
    {
        var scenario = await CreateScenarioAsync();

        var status = await MarkSentAsync(scenario);

        Assert.Equal(NotificationDeliveryMutationStatus.Applied, status);
        Assert.Equal("sent", await ReadStatusAsync(scenario.NotificationId));
        Assert.True(await HasSentAtAsync(scenario.NotificationId));
    }

    [Fact]
    public async Task MarkSent_WhenLeaseLost_DoesNotChangeTheNotification()
    {
        var scenario = await CreateScenarioAsync();

        var status = await MarkSentAsync(scenario, leaseOwnerId: Guid.NewGuid());

        // Esta é a garantia central do lease: o owner antigo não escreve nada.
        Assert.Equal(NotificationDeliveryMutationStatus.LeaseLost, status);
        Assert.Equal("pending", await ReadStatusAsync(scenario.NotificationId));
        Assert.False(await HasSentAtAsync(scenario.NotificationId));
    }

    [Fact]
    public async Task MarkSent_WhenRepeatedByTheSameOwner_IsAlreadyApplied()
    {
        var scenario = await CreateScenarioAsync();

        Assert.Equal(NotificationDeliveryMutationStatus.Applied, await MarkSentAsync(scenario));
        Assert.Equal(
            NotificationDeliveryMutationStatus.AlreadyApplied,
            await MarkSentAsync(scenario));
    }

    [Fact]
    public async Task MarkFailed_WhenLeaseValid_RecordsSanitizedCodeAndIncrementsRetry()
    {
        var scenario = await CreateScenarioAsync();

        var status = await MarkFailedAsync(scenario, "resend_http_503");

        Assert.Equal(NotificationDeliveryMutationStatus.Applied, status);
        Assert.Equal("failed", await ReadStatusAsync(scenario.NotificationId));
        Assert.Equal("resend_http_503", await ReadErrorMessageAsync(scenario.NotificationId));
        Assert.Equal(1, await ReadRetryCountAsync(scenario.NotificationId));
    }

    [Fact]
    public async Task MarkFailed_WhenLeaseLost_DoesNotChangeTheNotification()
    {
        var scenario = await CreateScenarioAsync();

        var status = await MarkFailedAsync(
            scenario, "resend_http_503", leaseOwnerId: Guid.NewGuid());

        Assert.Equal(NotificationDeliveryMutationStatus.LeaseLost, status);
        Assert.Equal("pending", await ReadStatusAsync(scenario.NotificationId));
        Assert.Null(await ReadErrorMessageAsync(scenario.NotificationId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Resend Said No")]
    [InlineData("code-with-dash")]
    [InlineData("provider returned 500: smtp://user:secret@host")]
    public async Task MarkFailed_RejectsCodesOutsideTheSanitizedAlphabet(string failureCode)
    {
        var scenario = await CreateScenarioAsync();

        // Um código não sanitizado poderia transportar dados do provider para uma
        // coluna que é lida em diagnóstico.
        await Assert.ThrowsAsync<ArgumentException>(
            () => MarkFailedAsync(scenario, failureCode));
    }

    [Fact]
    public async Task Operations_RejectEmptyIdentifiers()
    {
        var scenario = await CreateScenarioAsync();

        await Assert.ThrowsAsync<ArgumentException>(
            () => PrepareAsync(scenario with { NotificationId = Guid.Empty }));
        await Assert.ThrowsAsync<ArgumentException>(
            () => PrepareAsync(scenario with { JobId = Guid.Empty }));
        await Assert.ThrowsAsync<ArgumentException>(
            () => PrepareAsync(scenario, leaseOwnerId: Guid.Empty));
    }

    [Fact]
    public async Task Prepare_ForNotificationOfAnotherTenant_BehavesAsNotFound()
    {
        var owner = await CreateScenarioAsync();
        var otherTenant = await _fixture.SeedTenantWithClientAsync(
            $"delivery-other-{Guid.NewGuid():N}",
            TestContext.Current.CancellationToken);

        // O contexto é aberto no tenant errado: o Global Query Filter tem de
        // esconder a notificação em vez de a devolver.
        await using var context = _fixture.CreateContext(otherTenant.TrainerId);
        var store = new NotificationDeliveryStore(context, new TestClock(Now));

        var preparation = await store.PrepareAsync(
            owner.NotificationId,
            owner.JobId,
            owner.LeaseOwnerId,
            TestContext.Current.CancellationToken);

        Assert.Equal(NotificationDeliveryPreparationStatus.NotFound, preparation.Status);
        Assert.Equal("pending", await ReadStatusAsync(owner.NotificationId));
    }

    private async Task<NotificationDeliveryPreparation> PrepareAsync(
        DeliveryScenario scenario,
        Guid? leaseOwnerId = null)
    {
        await using var context = _fixture.CreateContext(scenario.TrainerId);
        var store = new NotificationDeliveryStore(context, new TestClock(Now));

        return await store.PrepareAsync(
            scenario.NotificationId,
            scenario.JobId,
            leaseOwnerId ?? scenario.LeaseOwnerId,
            TestContext.Current.CancellationToken);
    }

    private async Task<NotificationDeliveryMutationStatus> MarkSentAsync(
        DeliveryScenario scenario,
        Guid? leaseOwnerId = null)
    {
        await using var context = _fixture.CreateContext(scenario.TrainerId);
        var store = new NotificationDeliveryStore(context, new TestClock(Now));

        return await store.MarkSentAsync(
            scenario.NotificationId,
            scenario.JobId,
            leaseOwnerId ?? scenario.LeaseOwnerId,
            TestContext.Current.CancellationToken);
    }

    private async Task<NotificationDeliveryMutationStatus> MarkFailedAsync(
        DeliveryScenario scenario,
        string failureCode,
        Guid? leaseOwnerId = null)
    {
        await using var context = _fixture.CreateContext(scenario.TrainerId);
        var store = new NotificationDeliveryStore(context, new TestClock(Now));

        return await store.MarkFailedAsync(
            scenario.NotificationId,
            scenario.JobId,
            leaseOwnerId ?? scenario.LeaseOwnerId,
            failureCode,
            TestContext.Current.CancellationToken);
    }

    private async Task<DeliveryScenario> CreateScenarioAsync(DateTime? leaseExpiresAt = null)
    {
        var seed = await _fixture.SeedTenantWithClientAsync($"delivery-{Guid.NewGuid():N}");

        var notification = new Notification(
            seed.TrainerId,
            seed.ClientId,
            new EmailAddress($"recipient-{Guid.NewGuid():N}@example.test"),
            "session_reminder",
            "session_reminder",
            "{\"client_name\":\"Ana\"}",
            Now);

        var job = new DurableJob(
            seed.TrainerId,
            "send_notification",
            1,
            $"{{\"notification_id\":\"{notification.Id}\"}}",
            $"idem-{Guid.NewGuid():N}",
            Guid.NewGuid(),
            Now,
            Now);

        await using (var context = _fixture.CreateContext(seed.TrainerId))
        {
            context.Notifications.Add(notification);
            context.DurableJobs.Add(job);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // O claim é aplicado por SQL directo para reproduzir exactamente o estado
        // que o dispatcher deixa na base de dados.
        var leaseOwnerId = Guid.NewGuid();
        await _fixture.ExecuteSqlAsync(
            """
            UPDATE durable_jobs
            SET status = 'processing',
                lease_owner_id = @owner,
                lease_expires_at = @expires,
                attempts = 1
            WHERE id = @id
            """,
            TestContext.Current.CancellationToken,
            new NpgsqlParameter("owner", leaseOwnerId),
            new NpgsqlParameter("expires", leaseExpiresAt ?? Now.AddMinutes(1)),
            new NpgsqlParameter("id", job.Id));

        return new DeliveryScenario(
            seed.TrainerId, notification.Id, job.Id, leaseOwnerId);
    }

    private Task SetNotificationStatusAsync(Guid notificationId, string status) =>
        _fixture.ExecuteSqlAsync(
            "UPDATE notifications SET status = @status WHERE id = @id",
            TestContext.Current.CancellationToken,
            new NpgsqlParameter("status", status),
            new NpgsqlParameter("id", notificationId));

    private Task<string?> ReadStatusAsync(Guid notificationId) =>
        _fixture.QueryScalarAsync<string>(
            "SELECT status FROM notifications WHERE id = @id",
            TestContext.Current.CancellationToken,
            new NpgsqlParameter("id", notificationId));

    private Task<string?> ReadErrorMessageAsync(Guid notificationId) =>
        _fixture.QueryScalarAsync<string>(
            "SELECT error_message FROM notifications WHERE id = @id",
            TestContext.Current.CancellationToken,
            new NpgsqlParameter("id", notificationId));

    private Task<int> ReadRetryCountAsync(Guid notificationId) =>
        _fixture.QueryScalarAsync<int>(
            "SELECT retry_count FROM notifications WHERE id = @id",
            TestContext.Current.CancellationToken,
            new NpgsqlParameter("id", notificationId));

    private async Task<bool> HasSentAtAsync(Guid notificationId) =>
        await _fixture.QueryScalarAsync<long>(
            "SELECT COUNT(*) FROM notifications WHERE id = @id AND sent_at IS NOT NULL",
            TestContext.Current.CancellationToken,
            new NpgsqlParameter("id", notificationId)) == 1;

    private sealed record DeliveryScenario(
        Guid TrainerId,
        Guid NotificationId,
        Guid JobId,
        Guid LeaseOwnerId);
}
