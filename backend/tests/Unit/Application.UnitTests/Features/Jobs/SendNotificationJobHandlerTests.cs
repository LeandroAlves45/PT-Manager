using Application.Features.Jobs.Dispatching;
using Application.Features.Notifications.Delivery;

namespace Application.UnitTests.Features.Jobs;

/// <summary>
/// Verifica o handler do único durable job.
/// Cada estado de preparação tem um
/// significado operacional distinto. Confundir "já entregue" com "falha" produz
/// emails duplicados; confundir "payload inválido" com "falha transitória"
/// consome as cinco tentativas antes de desistir de um job que nunca poderia
/// ter sucesso.
/// </summary>
public sealed class SendNotificationJobHandlerTests
{
    private static readonly Guid NotificationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly FakeNotificationDeliveryStore _store = new();
    private readonly FakeNotificationDeliveryGateway _gateway = new();

    [Fact]
    public void Handler_DeclaresTheSingleSupportedRoute()
    {
        var handler = CreateHandler();

        Assert.Equal("send_notification", handler.JobType);
        Assert.Equal(1, handler.JobVersion);
    }

    [Fact]
    public async Task Handle_WhenDeliverySucceeds_MarksSentAndReusesJobIdempotencyKey()
    {
        _store.PreparationResult = ReadyPreparation();
        _gateway.Result = NotificationDeliveryOutcome.Sent();
        var job = CreateJob();

        var outcome = await CreateHandler().HandleAsync(job, CancellationToken.None);

        Assert.Equal(DispatchItemOutcomeKind.Succeeded, outcome.Kind);
        Assert.Equal(1, _store.MarkSentCalls);
        Assert.Equal(0, _store.MarkFailedCalls);
        // A chave persistida no job é a única coisa que torna o retry seguro no
        // lado do Resend, por isso não pode ser regenerada pelo handler.
        Assert.Equal(job.IdempotencyKey, _gateway.LastIdempotencyKey);
    }

    [Fact]
    public async Task Handle_WhenAlreadyDelivered_SucceedsWithoutCallingProvider()
    {
        _store.PreparationResult = NotificationDeliveryPreparation.FromStatus(
            NotificationDeliveryPreparationStatus.AlreadyDelivered);

        var outcome = await CreateHandler().HandleAsync(CreateJob(), CancellationToken.None);

        Assert.Equal(DispatchItemOutcomeKind.Succeeded, outcome.Kind);
        Assert.Equal(0, _gateway.Calls);
        Assert.Equal(0, _store.MarkSentCalls);
    }

    [Fact]
    public async Task Handle_WhenLeaseLostDuringPreparation_ReturnsLeaseLostWithoutProvider()
    {
        _store.PreparationResult = NotificationDeliveryPreparation.FromStatus(
            NotificationDeliveryPreparationStatus.LeaseLost);

        var outcome = await CreateHandler().HandleAsync(CreateJob(), CancellationToken.None);

        Assert.Equal(DispatchItemOutcomeKind.LeaseLost, outcome.Kind);
        Assert.Equal(0, _gateway.Calls);
    }

    [Fact]
    public async Task Handle_WhenNotificationMissing_FailsPermanently()
    {
        _store.PreparationResult = NotificationDeliveryPreparation.FromStatus(
            NotificationDeliveryPreparationStatus.NotFound);

        var outcome = await CreateHandler().HandleAsync(CreateJob(), CancellationToken.None);

        Assert.Equal(DispatchItemOutcomeKind.PermanentFailure, outcome.Kind);
        Assert.Equal("notification_not_found", outcome.FailureCode);
        Assert.Equal(0, _gateway.Calls);
    }

    [Fact]
    public async Task Handle_WhenStateInvalid_FailsPermanently()
    {
        _store.PreparationResult = NotificationDeliveryPreparation.FromStatus(
            NotificationDeliveryPreparationStatus.InvalidState);

        var outcome = await CreateHandler().HandleAsync(CreateJob(), CancellationToken.None);

        Assert.Equal(DispatchItemOutcomeKind.PermanentFailure, outcome.Kind);
        Assert.Equal("notification_state_invalid", outcome.FailureCode);
        Assert.Equal(0, _gateway.Calls);
    }

    [Fact]
    public async Task Handle_WhenProviderIsTransient_SchedulesRetryAndKeepsCode()
    {
        _store.PreparationResult = ReadyPreparation();
        _gateway.Result = NotificationDeliveryOutcome.TransientFailure("resend_http_503");

        var outcome = await CreateHandler().HandleAsync(CreateJob(), CancellationToken.None);

        Assert.Equal(DispatchItemOutcomeKind.TransientFailure, outcome.Kind);
        Assert.Equal("resend_http_503", outcome.FailureCode);
        Assert.Equal("resend_http_503", _store.LastFailureCode);
        Assert.Equal(1, _store.MarkFailedCalls);
    }

    [Fact]
    public async Task Handle_WhenProviderIsPermanent_DoesNotScheduleRetry()
    {
        _store.PreparationResult = ReadyPreparation();
        _gateway.Result = NotificationDeliveryOutcome.PermanentFailure("resend_http_422");

        var outcome = await CreateHandler().HandleAsync(CreateJob(), CancellationToken.None);

        Assert.Equal(DispatchItemOutcomeKind.PermanentFailure, outcome.Kind);
        Assert.Equal("resend_http_422", outcome.FailureCode);
    }

    [Fact]
    public async Task Handle_WhenLeaseLostWhileMarkingSent_ReturnsLeaseLost()
    {
        _store.PreparationResult = ReadyPreparation();
        _gateway.Result = NotificationDeliveryOutcome.Sent();
        _store.MarkSentResult = NotificationDeliveryMutationStatus.LeaseLost;

        var outcome = await CreateHandler().HandleAsync(CreateJob(), CancellationToken.None);

        Assert.Equal(DispatchItemOutcomeKind.LeaseLost, outcome.Kind);
    }

    [Fact]
    public async Task Handle_WhenMarkSentIsAlreadyApplied_IsIdempotentSuccess()
    {
        _store.PreparationResult = ReadyPreparation();
        _gateway.Result = NotificationDeliveryOutcome.Sent();
        _store.MarkSentResult = NotificationDeliveryMutationStatus.AlreadyApplied;

        var outcome = await CreateHandler().HandleAsync(CreateJob(), CancellationToken.None);

        Assert.Equal(DispatchItemOutcomeKind.Succeeded, outcome.Kind);
    }

    [Fact]
    public async Task Handle_WhenLeaseLostWhileMarkingFailure_ReturnsLeaseLost()
    {
        _store.PreparationResult = ReadyPreparation();
        _gateway.Result = NotificationDeliveryOutcome.TransientFailure("resend_http_500");
        _store.MarkFailedResult = NotificationDeliveryMutationStatus.LeaseLost;

        var outcome = await CreateHandler().HandleAsync(CreateJob(), CancellationToken.None);

        Assert.Equal(DispatchItemOutcomeKind.LeaseLost, outcome.Kind);
    }

    [Theory]
    [InlineData("{not-json")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"notification_id\":\"00000000-0000-0000-0000-000000000000\"}")]
    [InlineData("{\"notification_id\":\"11111111-1111-1111-1111-111111111111\",\"extra\":1}")]
    [InlineData("{\"notificationId\":\"11111111-1111-1111-1111-111111111111\"}")]
    public async Task Handle_InvalidPayload_FailsPermanentlyWithoutTouchingStoreOrProvider(
        string payload)
    {
        var job = CreateJob() with { Payload = payload };

        var outcome = await CreateHandler().HandleAsync(job, CancellationToken.None);

        Assert.Equal(DispatchItemOutcomeKind.PermanentFailure, outcome.Kind);
        Assert.Equal("notification_payload_invalid", outcome.FailureCode);
        // Um payload corrompido nunca pode alcançar o provider nem a base de dados.
        Assert.Equal(0, _store.PrepareCalls);
        Assert.Equal(0, _gateway.Calls);
    }

    [Theory]
    [InlineData("send_notification", 2)]
    [InlineData("billing_notification", 1)]
    public async Task Handle_UnsupportedRoute_FailsPermanentlyWithContractMismatch(
        string jobType,
        int jobVersion)
    {
        var job = CreateJob() with { JobType = jobType, JobVersion = jobVersion };

        var outcome = await CreateHandler().HandleAsync(job, CancellationToken.None);

        Assert.Equal(DispatchItemOutcomeKind.PermanentFailure, outcome.Kind);
        Assert.Equal("notification_job_contract_mismatch", outcome.FailureCode);
        Assert.Equal(0, _store.PrepareCalls);
        Assert.Equal(0, _gateway.Calls);
    }

    [Fact]
    public async Task Handle_NullJob_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => CreateHandler().HandleAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PassesJobLeaseOwnerToStore()
    {
        _store.PreparationResult = ReadyPreparation();
        var job = CreateJob();

        await CreateHandler().HandleAsync(job, CancellationToken.None);

        // O store condiciona todas as escritas a este par, por isso o handler não
        // pode substituí-lo por um valor derivado.
        Assert.Equal(job.Id, _store.LastJobId);
        Assert.Equal(job.LeaseOwnerId, _store.LastLeaseOwnerId);
    }

    private SendNotificationJobHandler CreateHandler() => new(_store, _gateway);

    private static NotificationDeliveryPreparation ReadyPreparation() =>
        NotificationDeliveryPreparation.Ready(new NotificationDeliveryMessage(
            NotificationId,
            "client@example.com",
            "session_reminder",
            "{\"client_name\":\"Ana\"}"));

    private static DurableJobEnvelope CreateJob() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "send_notification",
        1,
        $"{{\"notification_id\":\"{NotificationId}\"}}",
        "idem-key-001",
        Guid.NewGuid(),
        1,
        Guid.NewGuid());
}
