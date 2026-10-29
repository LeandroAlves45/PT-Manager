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
            AuthenticationLinkBuilder.ConfirmEmailPath,
            "confirm-email.html",
            "Confirma o teu email",
            "Obrigado por te registares no PT Manager. Para completares o registo e ativares a conta, " +
            "confirma o teu email.",
            "Confirmar email",
            cancellationToken);

    public Task<AuthenticationEmailDeliveryOutcome> SendClientInvitationAsync(
        IssuedAuthenticationSecret secret,
        CancellationToken cancellationToken) =>
        SendAsync(
            secret,
            AuthenticationLinkBuilder.AcceptInvitePath,
            "client-invitation.html",
            "Convite do teu personal trainer",
            "Foste convidado(a) para aceder ao PT Manager.",
            "Aceitar convite",
            cancellationToken);

    public Task<AuthenticationEmailDeliveryOutcome> SendTrainerInvitationAsync(
        IssuedAuthenticationSecret secret,
        CancellationToken cancellationToken) =>
        SendAsync(
            secret,
            AuthenticationLinkBuilder.TrainerInvitePath,
            "trainer-invitation.html",
            "Convite para o PT Manager",
            "Foste convidado(a) para usar o PT Manager como personal trainer.",
            "Criar conta",
            cancellationToken);

    public Task<AuthenticationEmailDeliveryOutcome> SendPasswordResetAsync(
        IssuedAuthenticationSecret secret,
        CancellationToken cancellationToken) =>
        SendAsync(
            secret,
            AuthenticationLinkBuilder.PasswordResetPath,
            "password-reset.html",
            "Reposição da palavra-passe",
            "Recebemos um pedido para repor a palavra-passe da tua conta. Se não foste tu, ignora este email.",
            "Repor palavra-passe",
            cancellationToken);

    public Task<AuthenticationEmailDeliveryOutcome> SendAccountAlreadyExistsAsync(
        string recipientEmail,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmail);

        var frontend = _options.FrontendBaseUrl!;
        return DeliverAsync(
            recipientEmail,
            AuthenticationLinkBuilder.Build(frontend, AuthenticationLinkBuilder.SignInPath),
            expiresAt: null,
            AuthenticationEmailTemplateRenderer.AccountAlreadyExistsTemplate,
            "Já existe uma conta com este email",
            "Alguém tentou criar uma conta no PT Manager com este email, mas já existe uma conta associada. " +
            "Se foste tu, entra na tua conta.",
            "Entrar",
            (AuthenticationLinkBuilder.Build(frontend, AuthenticationLinkBuilder.ForgotPasswordPath),
                "Recuperar palavra-passe"),
            cancellationToken);
    }

    private Task<AuthenticationEmailDeliveryOutcome> SendAsync(
        IssuedAuthenticationSecret secret,
        string path,
        string templateName,
        string subject,
        string intro,
        string actionLabel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(secret);

        return DeliverAsync(
            secret.RecipientEmail,
            AuthenticationLinkBuilder.Build(_options.FrontendBaseUrl!, path, secret.RawToken),
            secret.ExpiresAt,
            templateName,
            subject,
            intro,
            actionLabel,
            secondaryAction: null,
            cancellationToken);
    }

    private async Task<AuthenticationEmailDeliveryOutcome> DeliverAsync(
        string recipientEmail,
        string link,
        DateTime? expiresAt,
        string templateName,
        string subject,
        string intro,
        string actionLabel,
        (string Url, string Label)? secondaryAction,
        CancellationToken cancellationToken)
    {
        var rendered = AuthenticationEmailTemplateRenderer.Render(
            templateName,
            intro,
            link,
            actionLabel,
            expiresAt,
            new AuthenticationEmailTemplateOptions(),
            secondaryAction);

        var result = await ResendEmailTransport.SendAsync(
            _httpClient,
            new ResendEmailMessage(
                _options.FromAddress,
                [recipientEmail],
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
}
