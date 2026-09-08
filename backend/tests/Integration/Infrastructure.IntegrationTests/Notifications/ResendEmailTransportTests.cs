using System.Net;
using Infrastructure.Email;

namespace Infrastructure.IntegrationTests.Notifications;

/// <summary>
/// Verifica o transporte Resend partilhado.
/// A classificação de estados HTTP decide se um envio é repetido ou abandonado.
/// Tratar um 4xx definitivo como transitório gastaria todas as tentativas; tratar
/// um 429 como permanente perderia um email que só precisava de esperar.
/// </summary>
public sealed class ResendEmailTransportTests
{
    [Fact]
    public async Task Send_WhenProviderAccepts_ReportsSent()
    {
        using var client = CreateClient(HttpStatusCode.OK, out var handler);

        var outcome = await SendAsync(client, "idem-key-1");

        Assert.Equal(ResendTransportOutcomeKind.Sent, outcome.Kind);
        Assert.Null(outcome.FailureCode);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
    }

    [Fact]
    public async Task Send_ForwardsTheIdempotencyKeyVerbatim()
    {
        using var client = CreateClient(HttpStatusCode.OK, out var handler);

        await SendAsync(client, "job-idem-key-42");

        // A chave é o que impede o Resend de entregar duas vezes num retry.
        Assert.True(handler.LastRequest!.Headers.TryGetValues(
            "Idempotency-Key", out var values));
        Assert.Equal("job-idem-key-42", Assert.Single(values!));
    }

    [Fact]
    public async Task Send_WhenKeyIsNull_OmitsTheHeader()
    {
        using var client = CreateClient(HttpStatusCode.OK, out var handler);

        await SendAsync(client, idempotencyKey: null);

        Assert.False(handler.LastRequest!.Headers.Contains("Idempotency-Key"));
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout, 408)]
    [InlineData(HttpStatusCode.Conflict, 409)]
    [InlineData((HttpStatusCode)425, 425)]
    [InlineData(HttpStatusCode.TooManyRequests, 429)]
    [InlineData(HttpStatusCode.InternalServerError, 500)]
    [InlineData(HttpStatusCode.BadGateway, 502)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 503)]
    public async Task Send_TransientStatuses_AreRetryable(
        HttpStatusCode statusCode,
        int expectedCode)
    {
        using var client = CreateClient(statusCode, out _);

        var outcome = await SendAsync(client, "idem-key-1");

        Assert.Equal(ResendTransportOutcomeKind.TransientFailure, outcome.Kind);
        Assert.Equal($"resend_http_{expectedCode}", outcome.FailureCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400)]
    [InlineData(HttpStatusCode.Unauthorized, 401)]
    [InlineData(HttpStatusCode.Forbidden, 403)]
    [InlineData(HttpStatusCode.NotFound, 404)]
    [InlineData(HttpStatusCode.UnprocessableEntity, 422)]
    public async Task Send_PermanentStatuses_AreNotRetryable(
        HttpStatusCode statusCode,
        int expectedCode)
    {
        using var client = CreateClient(statusCode, out _);

        var outcome = await SendAsync(client, "idem-key-1");

        Assert.Equal(ResendTransportOutcomeKind.PermanentFailure, outcome.Kind);
        Assert.Equal($"resend_http_{expectedCode}", outcome.FailureCode);
    }

    [Fact]
    public async Task Send_WhenProviderIsUnreachable_IsTransient()
    {
        using var client = CreateFaultingClient(new HttpRequestException("no route"));

        var outcome = await SendAsync(client, "idem-key-1");

        Assert.Equal(ResendTransportOutcomeKind.TransientFailure, outcome.Kind);
        Assert.Equal("resend_unreachable", outcome.FailureCode);
    }

    [Fact]
    public async Task Send_WhenRequestTimesOut_IsTransient()
    {
        using var client = CreateFaultingClient(new TaskCanceledException("timeout"));

        var outcome = await SendAsync(client, "idem-key-1");

        Assert.Equal(ResendTransportOutcomeKind.TransientFailure, outcome.Kind);
        Assert.Equal("resend_timeout", outcome.FailureCode);
    }

    [Fact]
    public async Task Send_WhenCallerCancels_PropagatesCancellation()
    {
        using var client = CreateFaultingClient(new TaskCanceledException("cancelled"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Cancelamento do caller não é uma falha do provider e não pode virar retry.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ResendEmailTransport.SendAsync(
                client, CreateMessage(), "idem-key-1", cancellation.Token));
    }

    [Fact]
    public async Task Send_FailureCodeNeverCarriesProviderText()
    {
        using var client = CreateClient(
            HttpStatusCode.BadRequest,
            out _,
            responseBody: "{\"error\":\"api key sk_live_secret is invalid\"}");

        var outcome = await SendAsync(client, "idem-key-1");

        // O corpo da resposta não pode contaminar o código persistido.
        Assert.Equal("resend_http_400", outcome.FailureCode);
        Assert.DoesNotContain("sk_live", outcome.FailureCode!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("key-with-\nnewline")]
    [InlineData("key-with-\rcarriage")]
    [InlineData("chave-com-acento-é")]
    public async Task Send_RejectsUnsafeIdempotencyKeys(string idempotencyKey)
    {
        using var client = CreateClient(HttpStatusCode.OK, out _);

        // Um header com CR ou LF permitiria injecção de cabeçalhos.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ResendEmailTransport.SendAsync(
                client, CreateMessage(), idempotencyKey, CancellationToken.None));
    }

    [Fact]
    public async Task Send_RejectsKeyAboveMaximumLength()
    {
        using var client = CreateClient(HttpStatusCode.OK, out _);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ResendEmailTransport.SendAsync(
                client, CreateMessage(), new string('a', 257), CancellationToken.None));
    }

    [Fact]
    public async Task Send_RejectsIncompleteMessage()
    {
        using var client = CreateClient(HttpStatusCode.OK, out _);
        var incomplete = new ResendEmailMessage(
            "from@example.test", [], "Subject", "<p>html</p>", "text");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ResendEmailTransport.SendAsync(
                client, incomplete, "idem-key-1", CancellationToken.None));
    }

    private static Task<ResendTransportOutcome> SendAsync(
        HttpClient client,
        string? idempotencyKey) =>
        ResendEmailTransport.SendAsync(
            client,
            CreateMessage(),
            idempotencyKey,
            TestContext.Current.CancellationToken);

    private static ResendEmailMessage CreateMessage() => new(
        "no-reply@ptmanager.test",
        ["client@example.test"],
        "Lembrete da sua sessão",
        "<p>Olá</p>",
        "Olá");

    private static HttpClient CreateClient(
        HttpStatusCode statusCode,
        out StubHttpMessageHandler handler,
        string responseBody = "{\"id\":\"email-1\"}")
    {
        handler = new StubHttpMessageHandler(statusCode, responseBody);
        return new HttpClient(handler) { BaseAddress = new Uri("https://resend.test/") };
    }

    private static HttpClient CreateFaultingClient(Exception exception) =>
        new(new FaultingHttpMessageHandler(exception))
        {
            BaseAddress = new Uri("https://resend.test/")
        };

    /// <summary>Devolve uma resposta fixa e guarda o pedido recebido.</summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;

        public StubHttpMessageHandler(HttpStatusCode statusCode, string body)
        {
            _statusCode = statusCode;
            _body = body;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body)
            });
        }
    }

    /// <summary>Reproduz falhas de rede sem contactar nenhum servidor.</summary>
    private sealed class FaultingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public FaultingHttpMessageHandler(Exception exception) => _exception = exception;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw _exception;
        }
    }
}
