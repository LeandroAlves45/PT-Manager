using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Features.Billing.Notifications;
using Application.Features.Jobs.Dispatching;
using Infrastructure.Email;
using Infrastructure.Payments.Stripe;
using Microsoft.Extensions.Options;

namespace Infrastructure.Jobs;

/// <summary>Processa exclusivamente notificações de billing no outbox.</summary>
internal sealed class BillingNotificationOutboxHandler : IOutboxMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly IBillingNotificationRecipientStore _store;
    private readonly IBillingNotificationGateway _gateway;
    private readonly StripeOptions _options;

    public BillingNotificationOutboxHandler(
        IBillingNotificationRecipientStore store,
        IBillingNotificationGateway gateway,
        IOptions<StripeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public string MessageType => "billing_notification";

    public async Task<DispatchItemOutcome> HandleAsync(
        OutboxMessageEnvelope message,
        CancellationToken cancellationToken)
    {
        BillingPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<BillingPayload>(
                message.Payload,
                JsonOptions);
        }
        catch (JsonException)
        {
            return DispatchItemOutcome.PermanentFailure("billing_payload_invalid");
        }

        if (payload is null || payload.TrainerId == Guid.Empty ||
            payload.TrainerId != message.TrainerId ||
            string.IsNullOrWhiteSpace(payload.EventId) ||
            !IsSupportedKind(payload.Kind))
            return DispatchItemOutcome.PermanentFailure("billing_payload_invalid");

        if (_options.BillingManagementUrl is null)
            return DispatchItemOutcome.PermanentFailure("billing_management_url_missing");

        var email = await _store.GetEmailAsync(payload.TrainerId, cancellationToken);
        if (email is null)
            return DispatchItemOutcome.PermanentFailure("billing_recipient_not_found");

        var outcome = await _gateway.SendAsync(
            new BillingNotificationDelivery(
                email,
                payload.Kind,
                _options.BillingManagementUrl,
                message.IdempotencyKey),
            cancellationToken);

        return outcome.Status switch
        {
            BillingNotificationDeliveryStatus.Sent => DispatchItemOutcome.Succeeded(),
            BillingNotificationDeliveryStatus.TransientFailure =>
                DispatchItemOutcome.TransientFailure(outcome.FailureCode!),
            _ => DispatchItemOutcome.PermanentFailure(outcome.FailureCode!)
        };
    }

    private static bool IsSupportedKind(string kind) =>
        kind is BillingNotificationTemplateRenderer.PaymentFailedKind
            or BillingNotificationTemplateRenderer.TrialWillEndKind
            or BillingNotificationTemplateRenderer.SubscriptionActivatedKind
            or BillingNotificationTemplateRenderer.PaymentSucceededKind
            or BillingNotificationTemplateRenderer.SubscriptionCanceledKind;

    private sealed record BillingPayload(
        [property: JsonPropertyName("trainer_id")] Guid TrainerId,
        [property: JsonPropertyName("event_id")] string EventId,
        [property: JsonPropertyName("kind")] string Kind);
}
