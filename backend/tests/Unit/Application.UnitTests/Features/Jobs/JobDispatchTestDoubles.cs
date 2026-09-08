using Application.Features.Jobs.Dispatching;
using Application.Features.Notifications.Delivery;

namespace Application.UnitTests.Features.Jobs;

/// <summary>
/// Store de entrega controlável. Regista chamadas para que os testes possam
/// provar que uma transição proibida não chega sequer a ser tentada.
/// </summary>
internal sealed class FakeNotificationDeliveryStore : INotificationDeliveryStore
{
    public NotificationDeliveryPreparation PreparationResult { get; set; } =
        NotificationDeliveryPreparation.FromStatus(
            NotificationDeliveryPreparationStatus.NotFound);

    public NotificationDeliveryMutationStatus MarkSentResult { get; set; } =
        NotificationDeliveryMutationStatus.Applied;

    public NotificationDeliveryMutationStatus MarkFailedResult { get; set; } =
        NotificationDeliveryMutationStatus.Applied;

    public int PrepareCalls { get; private set; }
    public int MarkSentCalls { get; private set; }
    public int MarkFailedCalls { get; private set; }
    public string? LastFailureCode { get; private set; }
    public Guid LastLeaseOwnerId { get; private set; }
    public Guid LastJobId { get; private set; }

    public Task<NotificationDeliveryPreparation> PrepareAsync(
        Guid notificationId,
        Guid jobId,
        Guid leaseOwnerId,
        CancellationToken cancellationToken)
    {
        PrepareCalls++;
        LastJobId = jobId;
        LastLeaseOwnerId = leaseOwnerId;
        return Task.FromResult(PreparationResult);
    }

    public Task<NotificationDeliveryMutationStatus> MarkSentAsync(
        Guid notificationId,
        Guid jobId,
        Guid leaseOwnerId,
        CancellationToken cancellationToken)
    {
        MarkSentCalls++;
        return Task.FromResult(MarkSentResult);
    }

    public Task<NotificationDeliveryMutationStatus> MarkFailedAsync(
        Guid notificationId,
        Guid jobId,
        Guid leaseOwnerId,
        string failureCode,
        CancellationToken cancellationToken)
    {
        MarkFailedCalls++;
        LastFailureCode = failureCode;
        return Task.FromResult(MarkFailedResult);
    }
}

/// <summary>
/// Gateway de entrega controlável. Guarda a idempotency key recebida porque a
/// reutilização dessa chave entre tentativas é o que impede emails duplicados.
/// </summary>
internal sealed class FakeNotificationDeliveryGateway : INotificationDeliveryGateway
{
    public NotificationDeliveryOutcome Result { get; set; } =
        NotificationDeliveryOutcome.Sent();

    public int Calls { get; private set; }
    public string? LastIdempotencyKey { get; private set; }
    public NotificationDeliveryMessage? LastMessage { get; private set; }

    public Task<NotificationDeliveryOutcome> SendAsync(
        NotificationDeliveryMessage message,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        Calls++;
        LastMessage = message;
        LastIdempotencyKey = idempotencyKey;
        return Task.FromResult(Result);
    }
}
