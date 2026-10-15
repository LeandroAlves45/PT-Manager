using System.Net;
using System.Text.Json;
using Application.Features.Authentication.Abstractions;
using Infrastructure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Infrastructure.IntegrationTests.Identity;

public sealed class LoggingAuthenticationEmailSenderTests
{
    private static readonly IssuedAuthenticationSecret Secret = new(
        "trainer@example.test",
        "token with spaces",
        new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc));

    private static readonly IOptions<ResendOptions> Options = Microsoft.Extensions.Options.Options.Create(
        new ResendOptions
        {
            ApiKey = "not-used",
            FromAddress = "no-reply@example.test",
            FrontendBaseUrl = new Uri("https://app.example.test/")
        });

    [Fact]
    public async Task EveryEmailType_LogsTheSameLinkTheProviderWouldSend()
    {
        var logger = new CapturingLogger();
        var logging = new LoggingAuthenticationEmailSender(Options, logger);
        var recorder = new RecordingHandler();
        var resend = new ResendAuthenticationEmailSender(
            new HttpClient(recorder) { BaseAddress = new Uri("https://api.resend.test/") },
            Options,
            NullLogger<ResendAuthenticationEmailSender>.Instance);
        var cancellationToken = TestContext.Current.CancellationToken;

        var sends = new (string Type, Func<IAuthenticationEmailSender, Task<AuthenticationEmailDeliveryOutcome>> Send)[]
        {
            ("email_confirmation", sender => sender.SendEmailConfirmationAsync(Secret, cancellationToken)),
            ("client_invitation", sender => sender.SendClientInvitationAsync(Secret, cancellationToken)),
            ("password_reset", sender => sender.SendPasswordResetAsync(Secret, cancellationToken)),
            ("account_already_exists", sender => sender.SendAccountAlreadyExistsAsync(
                Secret.RecipientEmail, cancellationToken))
        };

        foreach (var (type, send) in sends)
        {
            logger.Messages.Clear();
            Assert.Equal(AuthenticationEmailDeliveryOutcome.Sent, await send(logging));
            Assert.Equal(AuthenticationEmailDeliveryOutcome.Sent, await send(resend));

            var logged = Assert.Single(logger.Messages);
            using var sent = JsonDocument.Parse(recorder.Body!);
            var providerText = sent.RootElement.GetProperty("text").GetString()!;
            var link = logged[(logged.IndexOf("https://", StringComparison.Ordinal))..];

            Assert.Contains(type, logged, StringComparison.Ordinal);
            Assert.Contains(Secret.RecipientEmail, logged, StringComparison.Ordinal);
            Assert.Contains(link, providerText, StringComparison.Ordinal);
        }
    }

    private sealed class CapturingLogger : ILogger<LoggingAuthenticationEmailSender>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
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
