using System.Net;
using System.Text;
using Api.Controllers;
using Api.FunctionalTests.Support;
using Application.Features.Billing.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Um 400 no webhook do Stripe tem de deixar nos logs o motivo, para se perceber em produção
/// se falta o segredo, se a versão da API está errada ou se o corpo é inválido. A assinatura
/// e o corpo nunca lá entram.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class BillingWebhookLoggingTests : IDisposable
{
    private const string WebhookPath = "/api/v1/billing/webhook";
    private const string SignatureSentinel = "t=1,v1=sentinel-signature-must-not-be-logged";
    private const string BodySentinel = "{\"sentinel\":\"body-must-not-be-logged\"}";

    private readonly CapturingLoggerProvider _logs = new();
    private readonly FakeAuthenticator _authenticator = new();
    private readonly ApiWebApplicationFactory _factory;

    public BillingWebhookLoggingTests(PostgresApiFixture fixture)
    {
        _factory = new ApiWebApplicationFactory(fixture.ConnectionString)
        {
            ConfigureServices = services =>
            {
                services.AddSingleton<ILoggerProvider>(_logs);
                services.RemoveAll<IPaymentWebhookAuthenticator>();
                services.AddSingleton<IPaymentWebhookAuthenticator>(_authenticator);
            }
        };
    }

    public void Dispose()
    {
        _factory.Dispose();
        _logs.Dispose();
    }

    [Theory]
    [InlineData(PaymentWebhookAuthenticationStatus.InvalidSignature, "invalid_signature")]
    [InlineData(PaymentWebhookAuthenticationStatus.InvalidPayload, "invalid_payload")]
    [InlineData(PaymentWebhookAuthenticationStatus.IncompatibleApiVersion, "incompatible_api_version")]
    public async Task Rejection_LogsTheReasonWithoutSignatureOrBody(
        PaymentWebhookAuthenticationStatus status, string reason)
    {
        _authenticator.Outcome = new PaymentWebhookAuthenticationOutcome(
            status,
            ReceivedApiVersion: status == PaymentWebhookAuthenticationStatus.IncompatibleApiVersion
                ? "2025-01-01.legacy"
                : null);

        var response = await SendAsync(SignatureSentinel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var entry = Assert.Single(Rejections());
        Assert.Equal(reason, entry.State["Reason"]);
        if (status == PaymentWebhookAuthenticationStatus.IncompatibleApiVersion)
            Assert.Equal("2025-01-01.legacy", entry.State["ReceivedApiVersion"]);
        AssertNoSecrets();
    }

    [Fact]
    public async Task MissingSignature_LogsTheReason()
    {
        var response = await SendAsync(signature: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("missing_signature", Assert.Single(Rejections()).State["Reason"]);
    }

    private IEnumerable<CapturedLogEntry> Rejections() => _logs.Snapshot().Where(entry =>
        entry.CategoryName == typeof(BillingController).FullName &&
        entry.LogLevel == LogLevel.Warning);

    private void AssertNoSecrets()
    {
        foreach (var entry in _logs.Snapshot())
        {
            Assert.DoesNotContain("sentinel-signature", entry.FormattedMessage, StringComparison.Ordinal);
            Assert.DoesNotContain("body-must-not-be-logged", entry.FormattedMessage, StringComparison.Ordinal);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(string? signature)
    {
        using var client = _factory.CreateOriginClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, WebhookPath)
        {
            Content = new StringContent(BodySentinel, Encoding.UTF8, "application/json")
        };
        if (signature is not null)
            request.Headers.Add("Stripe-Signature", signature);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private sealed class FakeAuthenticator : IPaymentWebhookAuthenticator
    {
        public PaymentWebhookAuthenticationOutcome Outcome { get; set; } =
            new(PaymentWebhookAuthenticationStatus.InvalidSignature);

        public PaymentWebhookAuthenticationOutcome Authenticate(ReadOnlyMemory<byte> body, string signatureHeader) =>
            Outcome;
    }
}
