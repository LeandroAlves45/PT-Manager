using Application.Features.Authentication.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Identity;

/// <summary>
/// Adapter de desenvolvimento: regista o link completo em log em vez de enviar o email.
/// Só é registado com <c>DevelopmentEmail:LogLinks</c>, que o arranque recusa fora de Development.
/// </summary>
internal sealed class LoggingAuthenticationEmailSender : IAuthenticationEmailSender
{
    private readonly Uri _frontendBaseUrl;
    private readonly ILogger<LoggingAuthenticationEmailSender> _logger;

    public LoggingAuthenticationEmailSender(
        IOptions<ResendOptions> options,
        ILogger<LoggingAuthenticationEmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _frontendBaseUrl = options.Value.FrontendBaseUrl
            ?? throw new ArgumentException("Resend:FrontendBaseUrl is required.", nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<AuthenticationEmailDeliveryOutcome> SendEmailConfirmationAsync(
        IssuedAuthenticationSecret secret,
        CancellationToken cancellationToken) =>
        Log("email_confirmation", secret, AuthenticationLinkBuilder.ConfirmEmailPath);

    public Task<AuthenticationEmailDeliveryOutcome> SendClientInvitationAsync(
        IssuedAuthenticationSecret secret,
        CancellationToken cancellationToken) =>
        Log("client_invitation", secret, AuthenticationLinkBuilder.AcceptInvitePath);

    public Task<AuthenticationEmailDeliveryOutcome> SendPasswordResetAsync(
        IssuedAuthenticationSecret secret,
        CancellationToken cancellationToken) =>
        Log("password_reset", secret, AuthenticationLinkBuilder.PasswordResetPath);

    public Task<AuthenticationEmailDeliveryOutcome> SendAccountAlreadyExistsAsync(
        string recipientEmail,
        CancellationToken cancellationToken)
    {
        LogLink("account_already_exists", recipientEmail,
            AuthenticationLinkBuilder.Build(_frontendBaseUrl, AuthenticationLinkBuilder.SignInPath));

        return Task.FromResult(AuthenticationEmailDeliveryOutcome.Sent);
    }

    private Task<AuthenticationEmailDeliveryOutcome> Log(
        string emailType,
        IssuedAuthenticationSecret secret,
        string path)
    {
        ArgumentNullException.ThrowIfNull(secret);

        LogLink(emailType, secret.RecipientEmail,
            AuthenticationLinkBuilder.Build(_frontendBaseUrl, path, secret.RawToken));

        return Task.FromResult(AuthenticationEmailDeliveryOutcome.Sent);
    }

    private void LogLink(string emailType, string recipient, string link) =>
        _logger.LogInformation(
            "Development email {EmailType} sent to {Recipient}: {Link}",
            emailType, recipient, link);
}
