using Application.Common.Abstractions;
using Application.Errors;
using Application.Features.Notifications;
using Application.Features.Notifications.Abstractions;
using Application.Features.Notifications.Dtos;
using Application.Features.Notifications.EnqueueNotification;

namespace Application.UnitTests.Features.Notifications;

/// <summary>Verifica a orquestração e o mapping de outcomes do handler de enqueue de notificações.</summary>
public sealed class NotificationHandlerTests
{
    private static readonly Guid TrainerId = Guid.NewGuid();
    private readonly StubClock _clock = new() { UtcNow = new DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc) };

    [Fact]
    public async Task Handle_ValidCommand_DerivesTenantAndPropagatesCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        var store = new FakeNotificationQueueStore();
        var handler = CreateHandler(store, CreateTenant());

        var result = await handler.HandleAsync(ValidCommand(), cancellationSource.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(TrainerId, store.LastRequest!.TrainerId);
        Assert.Equal(cancellationSource.Token, store.LastCancellationToken);
        Assert.Equal("pending", result.Value.Status);
    }

    [Fact]
    public async Task Handle_MissingTenant_FailsClosedWithoutCallingStore()
    {
        var store = new FakeNotificationQueueStore();
        var handler = CreateHandler(store, new StubTenantContext());

        var result = await handler.HandleAsync(ValidCommand(), CancellationToken.None);

        Assert.Equal("tenant_required", result.Error!.Code);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task Handle_ClientRole_ReturnsTrainerOnly()
    {
        var store = new FakeNotificationQueueStore();
        var tenant = new StubTenantContext { TrainerId = TrainerId, Role = "client" };
        var handler = CreateHandler(store, tenant);

        var result = await handler.HandleAsync(ValidCommand(), CancellationToken.None);

        Assert.Equal("notifications_trainer_only", result.Error!.Code);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
        Assert.Equal(0, store.Calls);
    }

    [Theory]
    [InlineData("{not-json")]
    [InlineData("[]")]
    [InlineData("{\"token\":\"secret\"}")]
    [InlineData("{\"nested\":{\"password\":\"secret\"}}")]
    public async Task Handle_UnsafeTemplateData_ReturnsValidation(string templateDataJson)
    {
        var store = new FakeNotificationQueueStore();
        var handler = CreateHandler(store, CreateTenant());

        var result = await handler.HandleAsync(
            ValidCommand() with { TemplateDataJson = templateDataJson },
            CancellationToken.None);

        Assert.Equal("validation_failed", result.Error!.Code);
        Assert.Contains(
            result.Error.ValidationErrors,
            error => error.Code == "notification_template_data_invalid");
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task Handle_AlreadyQueued_ReturnsOriginalNotificationAsSuccess()
    {
        var notificationId = Guid.NewGuid();
        var store = new FakeNotificationQueueStore
        {
            Result = NotificationQueueStoreResult.AlreadyQueued(notificationId, _clock.UtcNow)
        };
        var handler = CreateHandler(store, CreateTenant());

        var result = await handler.HandleAsync(ValidCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(notificationId, result.Value.NotificationId);
    }

    [Fact]
    public async Task Handle_ClientNotFound_ReturnsStableNotFound()
    {
        var store = new FakeNotificationQueueStore
        {
            Result = NotificationQueueStoreResult.ClientNotFound()
        };
        var handler = CreateHandler(store, CreateTenant());

        var result = await handler.HandleAsync(ValidCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("notification_client_not_found", result.Error!.Code);
    }

    private EnqueueNotificationHandler CreateHandler(
        FakeNotificationQueueStore store,
        StubTenantContext tenantContext)
    {
        return new EnqueueNotificationHandler(
            new EnqueueNotificationCommandValidator(_clock),
            tenantContext,
            _clock,
            store);
    }

    private static StubTenantContext CreateTenant() => new() { TrainerId = TrainerId };

    private static EnqueueNotificationCommand ValidCommand()
    {
        return new EnqueueNotificationCommand(
            ClientId: null,
            RecipientEmail: "recipient@example.test",
            NotificationType: "account",
            TemplateKey: "email_confirmation",
            TemplateDataJson: "{\"user_id\":\"11111111-1111-1111-1111-111111111111\"}",
            OperationKey: "confirm:22222222222222222222222222222222",
            CorrelationId: Guid.NewGuid(),
            ScheduledAt: null);
    }
}
