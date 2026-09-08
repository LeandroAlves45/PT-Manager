using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Features.Notifications.Delivery;
using Infrastructure.Email;
using Infrastructure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Notifications;

/// <summary>Entrega notificações de negócio através do Resend.</summary>
internal sealed class ResendNotificationDeliveryGateway : INotificationDeliveryGateway
{
    private const string SessionReminderTemplate = "session_reminder";
    private const int MaximumTemplateDataLength = 8 * 1024;

    private static readonly JsonSerializerOptions TemplateDataOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly HttpClient _httpClient;
    private readonly ResendOptions _options;
    private readonly ILogger<ResendNotificationDeliveryGateway> _logger;

    public ResendNotificationDeliveryGateway(
        HttpClient httpClient,
        IOptions<ResendOptions> options,
        ILogger<ResendNotificationDeliveryGateway> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<NotificationDeliveryOutcome> SendAsync(
        NotificationDeliveryMessage message,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!string.Equals(message.TemplateKey, SessionReminderTemplate, StringComparison.Ordinal))
            return NotificationDeliveryOutcome.PermanentFailure(
                "notification_template_not_supported");

        var data = DeserializeTemplateData(message.TemplateDataJson);
        if (data is null)
            return NotificationDeliveryOutcome.PermanentFailure(
                "notification_template_data_invalid");

        RenderedNotificationEmail rendered;
        try
        {
            rendered = SessionReminderTemplateRenderer.Render(data);
        }
        catch (ArgumentException)
        {
            return NotificationDeliveryOutcome.PermanentFailure(
                "notification_template_data_invalid");
        }
        catch (TimeZoneNotFoundException)
        {
            return NotificationDeliveryOutcome.PermanentFailure(
                "notification_timezone_invalid");
        }
        catch (InvalidTimeZoneException)
        {
            return NotificationDeliveryOutcome.PermanentFailure(
                "notification_timezone_invalid");
        }

        var transport = await ResendEmailTransport.SendAsync(
            _httpClient,
            new ResendEmailMessage(
                _options.FromAddress,
                [message.RecipientEmail],
                rendered.Subject,
                rendered.Html,
                rendered.Text),
            idempotencyKey,
            cancellationToken);

        if (transport.Kind == ResendTransportOutcomeKind.Sent)
            return NotificationDeliveryOutcome.Sent();

        _logger.LogWarning(
            "Business notification delivery failed with code {FailureCode}.",
            transport.FailureCode);

        return transport.Kind == ResendTransportOutcomeKind.TransientFailure
            ? NotificationDeliveryOutcome.TransientFailure(transport.FailureCode!)
            : NotificationDeliveryOutcome.PermanentFailure(transport.FailureCode!);
    }

    private static SessionReminderTemplateData? DeserializeTemplateData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumTemplateDataLength)
            return null;

        try
        {
            return JsonSerializer.Deserialize<SessionReminderTemplateData>(
                json, TemplateDataOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
