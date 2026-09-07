using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Features.Jobs.Dispatching;

namespace Application.Features.Notifications.Delivery;

/// <summary>Entrega uma notificação persistida através da porta de negócio.</summary>
public sealed class SendNotificationJobHandler : IDurableJobHandler
{
    private const string SupportedJobType = "send_notification";
    private const int SupportedJobVersion = 1;

    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly INotificationDeliveryStore _store;
    private readonly INotificationDeliveryGateway _gateway;

    public SendNotificationJobHandler(
        INotificationDeliveryStore store,
        INotificationDeliveryGateway gateway)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    }

    public string JobType => SupportedJobType;
    public int JobVersion => SupportedJobVersion;

    public async Task<DispatchItemOutcome> HandleAsync(
        DurableJobEnvelope job,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (job.JobType != SupportedJobType || job.JobVersion != SupportedJobVersion)
            return DispatchItemOutcome.PermanentFailure("notification_job_contract_mismatch");

        var payload = DeserializePayload(job.Payload);
        if (payload is null || payload.NotificationId == Guid.Empty)
            return DispatchItemOutcome.PermanentFailure("notification_payload_invalid");

        var preparation = await _store.PrepareAsync(
            payload.NotificationId,
            job.Id,
            job.LeaseOwnerId,
            cancellationToken);

        if (preparation.Status == NotificationDeliveryPreparationStatus.LeaseLost)
            return DispatchItemOutcome.LeaseLost();

        if (preparation.Status == NotificationDeliveryPreparationStatus.AlreadyDelivered)
            return DispatchItemOutcome.Succeeded();

        if (preparation.Status == NotificationDeliveryPreparationStatus.NotFound)
            return DispatchItemOutcome.PermanentFailure("notification_not_found");

        if (preparation.Status != NotificationDeliveryPreparationStatus.Ready ||
            preparation.Message is null)
            return DispatchItemOutcome.PermanentFailure("notification_state_invalid");

        var delivery = await _gateway.SendAsync(
            preparation.Message,
            job.IdempotencyKey,
            cancellationToken);

        return delivery.Kind == NotificationDeliveryOutcomeKind.Sent
            ? await CompleteSentAsync(job, payload.NotificationId, cancellationToken)
            : await CompleteFailedAsync(
                job,
                payload.NotificationId,
                delivery,
                cancellationToken);
    }

    private async Task<DispatchItemOutcome> CompleteSentAsync(
        DurableJobEnvelope job,
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var mutation = await _store.MarkSentAsync(
            notificationId,
            job.Id,
            job.LeaseOwnerId,
            cancellationToken);

        return mutation switch
        {
            NotificationDeliveryMutationStatus.Applied or
            NotificationDeliveryMutationStatus.AlreadyApplied =>
                DispatchItemOutcome.Succeeded(),
            NotificationDeliveryMutationStatus.LeaseLost => DispatchItemOutcome.LeaseLost(),
            _ => DispatchItemOutcome.PermanentFailure("notification_state_invalid")
        };
    }

    private async Task<DispatchItemOutcome> CompleteFailedAsync(
        DurableJobEnvelope job,
        Guid notificationId,
        NotificationDeliveryOutcome delivery,
        CancellationToken cancellationToken)
    {
        var failureCode = delivery.FailureCode ?? "notification_delivery_failed";
        var mutation = await _store.MarkFailedAsync(
            notificationId,
            job.Id,
            job.LeaseOwnerId,
            failureCode,
            cancellationToken);

        if (mutation == NotificationDeliveryMutationStatus.LeaseLost)
            return DispatchItemOutcome.LeaseLost();

        if (mutation == NotificationDeliveryMutationStatus.InvalidState)
            return DispatchItemOutcome.PermanentFailure("notification_state_invalid");

        return delivery.Kind == NotificationDeliveryOutcomeKind.TransientFailure
            ? DispatchItemOutcome.TransientFailure(failureCode)
            : DispatchItemOutcome.PermanentFailure(failureCode);
    }

    private static SendNotificationJobPayload? DeserializePayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        try
        {
            return JsonSerializer.Deserialize<SendNotificationJobPayload>(
                payload,
                PayloadOptions);
        }
        catch (JsonException)
        {
            // Payload persistido inválido é uma falha permanente e não uma exceção
            // repetível que consumiria todas as tentativas.
            return null;
        }
    }

    private sealed record SendNotificationJobPayload(
        [property: JsonPropertyName("notification_id")] Guid NotificationId);
}
