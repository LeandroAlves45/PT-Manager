using Infrastructure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Email;

/// <summary>Envia um email já renderizado; o resultado diz só se pode voltar a tentar.</summary>
internal interface IRenderedEmailSender
{
    Task<ResendTransportOutcome> SendAsync(
        string recipientEmail,
        RenderedNotificationEmail email,
        string idempotencyKey,
        CancellationToken cancellationToken);
}

/// <summary>Envia pelo Resend, passando a chave de idempotência do outbox.</summary>
internal sealed class ResendRenderedEmailSender : IRenderedEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly ResendOptions _options;

    public ResendRenderedEmailSender(HttpClient httpClient, IOptions<ResendOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<ResendTransportOutcome> SendAsync(
        string recipientEmail,
        RenderedNotificationEmail email,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);

        return ResendEmailTransport.SendAsync(
            _httpClient,
            new ResendEmailMessage(_options.FromAddress, [recipientEmail], email.Subject, email.Html, email.Text),
            idempotencyKey,
            cancellationToken);
    }
}

/// <summary>
/// Adapter de desenvolvimento: escreve no log o destinatário, o assunto e o texto, sem enviar.
/// Só é registado com <c>DevelopmentEmail:LogLinks</c>, que o arranque recusa fora de Development.
/// </summary>
internal sealed class LoggingRenderedEmailSender : IRenderedEmailSender
{
    private readonly ILogger<LoggingRenderedEmailSender> _logger;

    public LoggingRenderedEmailSender(ILogger<LoggingRenderedEmailSender> logger) =>
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public Task<ResendTransportOutcome> SendAsync(
        string recipientEmail,
        RenderedNotificationEmail email,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);

        _logger.LogInformation(
            "Development email sent to {Recipient}: {Subject} | {Text}",
            recipientEmail,
            email.Subject,
            email.Text);

        return Task.FromResult(ResendTransportOutcome.Sent());
    }
}
