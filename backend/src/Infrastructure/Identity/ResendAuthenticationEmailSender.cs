using Application.Features.Authentication.Abstractions;
using Infrastructure.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Identity;

/// <summary>Entrega os emails de autenticação através da API do Resend.</summary>
internal sealed class ResendAuthenticationEmailSender : IAuthenticationEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly ResendOptions _options;
    private readonly ILogger<ResendAuthenticationEmailSender> _logger;

    public ResendAuthenticationEmailSender(
        HttpClient httpClient,
        IOptions<ResendOptions> options,
        ILogger<ResendAuthenticationEmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<AuthenticationEmailDeliveryOutcome> SendEmailConfirmationAsync(
        IssuedAuthenticationSecret secret,
        CancellationToken cancellationToken) =>
        SendAsync(
            secret,
            "confirmar-email",
            "confirm-email.html",
            "Confirme o seu email",
            "Obrigado por se registar no PT Manager. Para completar o registo e ativar a conta, " +
            "confirme o email abaixo.",
            "Confirmar email",
            cancellationToken);

    public Task<AuthenticationEmailDeliveryOutcome> SendClientInvitationAsync(
        IssuedAuthenticationSecret secret,
        CancellationToken cancellationToken) =>
        SendAsync(
            secret,
            "aceitar-convite",
            "client-invitation.html",
            "Convite do seu personal trainer",
            "Foi convidado para aceder ao PT Manager.",
            "Aceitar convite",
            cancellationToken);

    public Task<AuthenticationEmailDeliveryOutcome> SendPasswordResetAsync(
        IssuedAuthenticationSecret secret,
        CancellationToken cancellationToken) =>
        SendAsync(
            secret,
            "repor-password",
            "password-reset.html",
            "Reposição de password",
            "Recebemos um pedido para repor a password da sua conta. Se não foi você, ignore este email.",
            "Repor password",
            cancellationToken);

    private async Task<AuthenticationEmailDeliveryOutcome> SendAsync(
        IssuedAuthenticationSecret secret,
        string path,
        string templateName,
        string subject,
        string intro,
        string actionLabel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(secret);

        var link = BuildLink(path, secret.RawToken);
        var rendered = AuthenticationEmailTemplateRenderer.Render(
            templateName,
            intro,
            link,
            actionLabel,
            secret.ExpiresAt,
            BuildTemplateOptions(templateName, secret));

        var result = await ResendEmailTransport.SendAsync(
            _httpClient,
            new ResendEmailMessage(
                _options.FromAddress,
                [secret.RecipientEmail],
                subject,
                rendered.Html,
                rendered.Text),
            idempotencyKey: null,
            cancellationToken);

        if (result.Kind == ResendTransportOutcomeKind.Sent)
            return AuthenticationEmailDeliveryOutcome.Sent;

        if (result.StatusCode.HasValue)
        {
            _logger.LogWarning(
                "Authentication email delivery failed with status code {StatusCode}.",
                result.StatusCode.Value);
        }
        else
        {
            _logger.LogWarning(
                "Authentication email delivery failed because the provider was unreachable.");
        }

        return AuthenticationEmailDeliveryOutcome.Unavailable;
    }

    private string BuildLink(string path, string rawToken) =>
        new UriBuilder(new Uri(_options.FrontendBaseUrl!, path))
        {
            Query = $"token={Uri.EscapeDataString(rawToken)}"
        }.Uri.ToString();

    // O contrato visual opcional continua isolado do transporte HTTP partilhado.
    private static AuthenticationEmailTemplateOptions BuildTemplateOptions(
        string templateName,
        IssuedAuthenticationSecret secret) => new();
}
