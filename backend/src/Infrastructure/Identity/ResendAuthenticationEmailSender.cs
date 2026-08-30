using System.Net.Http.Json;
using Application.Features.Authentication.Abstractions;
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
            "Confirme o seu email",
            "Confirme o seu email para ativar a conta.",
            cancellationToken);

    public Task<AuthenticationEmailDeliveryOutcome> SendClientInvitationAsync(
        IssuedAuthenticationSecret secret,
        CancellationToken cancellationToken) =>
        SendAsync(
            secret,
            "aceitar-convite",
            "Convite do seu personal trainer",
            "Aceite o convite para aceder ao seu plano.",
            cancellationToken);

    public Task<AuthenticationEmailDeliveryOutcome> SendPasswordResetAsync(
        IssuedAuthenticationSecret secret,
        CancellationToken cancellationToken) =>
        SendAsync(
            secret,
            "repor-password",
            "Reposição de password",
            "Reponha a sua password.",
            cancellationToken);

    private async Task<AuthenticationEmailDeliveryOutcome> SendAsync(
        IssuedAuthenticationSecret secret,
        string path,
        string subject,
        string intro,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(secret);

        var link = BuildLink(path, secret.RawToken);
        var payload = new ResendEmailRequest(
            _options.FromAddress,
            [secret.RecipientEmail],
            subject,
            BuildHtmlBody(intro, link, secret.ExpiresAt)
        );

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "emails",
                payload,
                cancellationToken);

            if (response.IsSuccessStatusCode)
                return AuthenticationEmailDeliveryOutcome.Sent;

            _logger.LogWarning(
                "Authentication email delivery failed with status code {StatusCode}.",
                (int)response.StatusCode);

            return AuthenticationEmailDeliveryOutcome.Unavailable;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "Authentication email delivery failed because the provider was unreachable.");

            return AuthenticationEmailDeliveryOutcome.Unavailable;
        }

        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout do HTTPClient, não cancelamento do pedido do utilizador
            _logger.LogWarning("Authentication email delivery timed out.");
            return AuthenticationEmailDeliveryOutcome.Unavailable;
        }
    }

    private string BuildLink(string path, string rawToken) =>
        new UriBuilder(new Uri(_options.FrontendBaseUrl!, path))
        {
            Query = $"token={Uri.EscapeDataString(rawToken)}"
        }.Uri.ToString();

    // TODO: Criar um template para o corpo do email em HTML, posteriormente.
    private static string BuildHtmlBody(string intro, string link, DateTimeOffset expiresAt) =>
        $"""
        <p>{intro}</p>
        <p><a href="{link}">Continuar</a></p>
        <p>Este link expira em {expiresAt:yyyy-MM-dd HH:mm} UTC.</p>
        """;

    /// <summary>Corpo do pedido aceite pela API do Resend.</summary>
    private sealed record ResendEmailRequest(
        string From,
        IReadOnlyList<string> To,
        string Subject,
        string Html);
}
