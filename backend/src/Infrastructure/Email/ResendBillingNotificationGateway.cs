using Application.Features.Billing.Notifications;
using Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace Infrastructure.Email;

/// <summary>Reutiliza o transporte Resend e preserva a key da outbox.</summary>
internal sealed class ResendBillingNotificationGateway : IBillingNotificationGateway
{
    private readonly HttpClient _httpClient;
    private readonly ResendOptions _options;

    public ResendBillingNotificationGateway(
        HttpClient httpClient,
        IOptions<ResendOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<BillingNotificationDeliveryOutcome> SendAsync(
        BillingNotificationDelivery delivery,
        CancellationToken cancellationToken)
    {
        RenderedBillingEmail rendered;
        try
        {
            rendered = BillingNotificationTemplateRenderer.Render(
                delivery.Kind,
                delivery.BillingManagementUrl);
        }
        catch (ArgumentOutOfRangeException)
        {
            return new(
                BillingNotificationDeliveryStatus.PermanentFailure,
                "billing_kind_not_supported");
        }

        var outcome = await ResendEmailTransport.SendAsync(
            _httpClient,
            new ResendEmailMessage(
                _options.FromAddress,
                [delivery.RecipientEmail],
                rendered.Subject,
                rendered.Html,
                rendered.Text
            ),
            delivery.IdempotencyKey,
            cancellationToken);

        return outcome.Kind switch
        {
            ResendTransportOutcomeKind.Sent => new(BillingNotificationDeliveryStatus.Sent),
            ResendTransportOutcomeKind.TransientFailure => new(
                BillingNotificationDeliveryStatus.TransientFailure,
                outcome.FailureCode),
            _ => new(BillingNotificationDeliveryStatus.PermanentFailure, outcome.FailureCode)
        };
    }
}
