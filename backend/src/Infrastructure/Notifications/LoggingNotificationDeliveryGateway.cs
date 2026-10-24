using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Features.Notifications.Delivery;
using Infrastructure.Email;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Notifications;

/// <summary>
/// Adapter de desenvolvimento: renderiza o lembrete de sessão como o Resend o enviaria e
/// escreve no log o destinatário, o assunto e o texto, sem enviar nada. Só é registado com
/// <c>DevelopmentEmail:LogLinks</c>, que o arranque recusa fora de Development.
/// </summary>
internal sealed class LoggingNotificationDeliveryGateway : INotificationDeliveryGateway
{
    private static readonly JsonSerializerOptions TemplateDataOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly ILogger<LoggingNotificationDeliveryGateway> _logger;

    public LoggingNotificationDeliveryGateway(ILogger<LoggingNotificationDeliveryGateway> logger) =>
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public Task<NotificationDeliveryOutcome> SendAsync(
        NotificationDeliveryMessage message,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!SessionReminderTemplates.IsSessionReminder(message.TemplateKey))
            return Task.FromResult(NotificationDeliveryOutcome.PermanentFailure(
                "notification_template_not_supported"));

        SessionReminderTemplateData? data;
        try
        {
            data = JsonSerializer.Deserialize<SessionReminderTemplateData>(
                message.TemplateDataJson ?? string.Empty, TemplateDataOptions);
        }
        catch (JsonException)
        {
            data = null;
        }

        if (data is null)
            return Task.FromResult(NotificationDeliveryOutcome.PermanentFailure(
                "notification_template_data_invalid"));

        var rendered = SessionReminderTemplateRenderer.Render(
            data,
            forTrainer: message.TemplateKey == SessionReminderTemplates.Trainer);

        _logger.LogInformation(
            "Development notification {TemplateKey} sent to {Recipient}: {Subject} | {Text}",
            message.TemplateKey,
            message.RecipientEmail,
            rendered.Subject,
            rendered.Text);

        return Task.FromResult(NotificationDeliveryOutcome.Sent());
    }
}
