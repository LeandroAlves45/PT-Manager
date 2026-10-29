using System.Net;
using System.Text.Json;
using Application.Features.Authentication.Abstractions;
using Infrastructure.Email;
using Infrastructure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Infrastructure.IntegrationTests.Identity;

public sealed class AuthenticationEmailTemplateTests
{
    [Fact]
    public void Render_WithHtmlInput_EncodesDynamicValues()
    {
        var rendered = AuthenticationEmailTemplateRenderer.Render(
            "confirm-email.html", "<script>alert(1)</script>",
            "https://app.example.test/confirmar-email?token=a&b", "Confirmar", DateTime.UtcNow);

        Assert.DoesNotContain("<script>", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("#0369a1", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("PT Manager", rendered.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WithPlaceholderInRecipientName_KeepsItAsText()
    {
        var rendered = AuthenticationEmailTemplateRenderer.Render(
            "confirm-email.html", "Intro", "https://app.example.test/confirmar-email?token=secret",
            "Confirmar", DateTime.UtcNow,
            new AuthenticationEmailTemplateOptions(RecipientName: "{{action_url}}"));

        Assert.Contains("Olá <strong>{{action_url}}</strong>", rendered.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WithTokenContainingSpaces_EncodesActionUrlInHtmlAndText()
    {
        const string actionUrl = "https://app.example.test/confirmar-email?token=token%20with%20spaces";
        var rendered = AuthenticationEmailTemplateRenderer.Render(
            "confirm-email.html",
            "Intro",
            actionUrl,
            "Confirmar",
            DateTime.UtcNow);

        Assert.Contains("token%20with%20spaces", rendered.Html, StringComparison.Ordinal);
        Assert.Contains(actionUrl, rendered.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendEmailConfirmation_EmitsHtmlAndTextPayload()
    {
        var handler = new RecordingHandler();
        var sender = CreateSender(handler);

        var outcome = await sender.SendEmailConfirmationAsync(
            new IssuedAuthenticationSecret(
                "trainer@example.test", "token with spaces",
                new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc)),
            TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(handler.Body!);
        var root = document.RootElement;
        Assert.Equal(
            (AuthenticationEmailDeliveryOutcome.Sent, true, true),
            (outcome, root.TryGetProperty("html", out _), root.TryGetProperty("text", out _)));
    }

    [Fact]
    public void Render_WithUnknownTemplate_FailsClosed()
    {
        var action = () => AuthenticationEmailTemplateRenderer.Render(
            "unknown.html", "Intro", "https://app.example.test", "Continuar", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public async Task SendEmailConfirmation_WhenProviderFails_NeverLogsSecretsOrLink()
    {
        const string rawToken = "super-secret-token-value";
        var logger = new CapturingLogger();
        var sender = CreateSender(
            new StatusHandler(HttpStatusCode.InternalServerError), logger);

        await sender.SendEmailConfirmationAsync(
            new IssuedAuthenticationSecret(
                "trainer@example.test", rawToken,
                new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc)),
            TestContext.Current.CancellationToken);

        var log = string.Join('\n', logger.Messages);
        Assert.DoesNotContain(rawToken, log, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("trainer@example.test", log, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("confirmar-email", log, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_AccountAlreadyExists_ResolvesEveryPlaceholderWithoutExpiry()
    {
        var rendered = AuthenticationEmailTemplateRenderer.Render(
            AuthenticationEmailTemplateRenderer.AccountAlreadyExistsTemplate,
            "Já existe uma conta.",
            "https://app.example.test/entrar",
            "Entrar",
            expiresAt: null,
            secondaryAction: ("https://app.example.test/recuperar-password", "Recuperar password"));

        Assert.DoesNotContain("{{", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("https://app.example.test/entrar", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("https://app.example.test/recuperar-password", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("https://app.example.test/recuperar-password", rendered.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("expira", rendered.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expira", rendered.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_AccountAlreadyExistsWithoutSecondaryAction_FailsClosed()
    {
        var action = () => AuthenticationEmailTemplateRenderer.Render(
            AuthenticationEmailTemplateRenderer.AccountAlreadyExistsTemplate,
            "Intro", "https://app.example.test/entrar", "Entrar", expiresAt: null);

        Assert.Throws<InvalidOperationException>(action);
    }

    [Theory]
    [InlineData("confirm-email.html")]
    [InlineData("client-invitation.html")]
    [InlineData("trainer-invitation.html")]
    [InlineData("password-reset.html")]
    public void Render_LinkTemplatesStillRequireAnExpiry(string template)
    {
        var action = () => AuthenticationEmailTemplateRenderer.Render(
            template, "Intro", "https://app.example.test/x", "Continuar", expiresAt: null);

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public async Task SendAccountAlreadyExists_PointsToSignInAndRecoveryWithoutAnyToken()
    {
        var handler = new RecordingHandler();
        var sender = CreateSender(handler);

        var outcome = await sender.SendAccountAlreadyExistsAsync(
            "trainer@example.test", TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(handler.Body!);
        var root = document.RootElement;
        var text = root.GetProperty("text").GetString()!;
        Assert.Equal(AuthenticationEmailDeliveryOutcome.Sent, outcome);
        Assert.Equal("trainer@example.test", root.GetProperty("to")[0].GetString());
        Assert.Contains("https://app.example.test/entrar", text, StringComparison.Ordinal);
        Assert.Contains("https://app.example.test/recuperar-password", text, StringComparison.Ordinal);
        Assert.DoesNotContain("token=", text, StringComparison.Ordinal);
    }

    private static ResendAuthenticationEmailSender CreateSender(
        HttpMessageHandler handler,
        ILogger<ResendAuthenticationEmailSender>? logger = null)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.test/") };
        var options = Options.Create(new ResendOptions
        {
            ApiKey = "not-used",
            FromAddress = "no-reply@example.test",
            FrontendBaseUrl = new Uri("https://app.example.test/")
        });
        return new ResendAuthenticationEmailSender(
            client, options, logger ?? NullLogger<ResendAuthenticationEmailSender>.Instance);
    }

    private sealed class StatusHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }

    private sealed class CapturingLogger : ILogger<ResendAuthenticationEmailSender>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            if (exception is not null)
                Messages.Add(exception.ToString());
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
