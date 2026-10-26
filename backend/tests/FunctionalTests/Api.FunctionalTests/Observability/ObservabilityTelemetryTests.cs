using System.Diagnostics;
using System.Net;
using System.Text;
using Api.FunctionalTests.Support;
using Application.Common.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Sentry;
using Sentry.AspNetCore;
using Sentry.Extensibility;
using Sentry.Protocol.Envelopes;
using TestSupport.Observability;

namespace Api.FunctionalTests.Observability;

/// <summary>
/// Gate 7A: nada sensível sai nos logs, nas spans nem nos eventos Sentry, as métricas
/// respondem às perguntas do 7B e um exporter em baixo não parte o negócio.
/// </summary>
/// <remarks>
/// O endpoint OTLP e o DSN apontam para a porta 9 do loopback, onde nada escuta: cada
/// exportação falha durante toda a classe. As spans chegam ao teste por um exporter em
/// memória acrescentado ao mesmo pipeline, depois do sampler e do filtro de health.
/// </remarks>
[Collection(ApiTestCollection.Name)]
public sealed class ObservabilityTelemetryTests : IAsyncLifetime
{
    private const string PasswordSentinel = "OBS-PASSWORD-SENTINEL-7A";
    private const string EmailSentinel = "obs-email-sentinel-7a@example.test";
    private const string TokenSentinel = "OBS-TOKEN-SENTINEL-7A";
    private const string CookieSentinel = "OBS-COOKIE-SENTINEL-7A";
    private const string BearerSentinel = "OBS-BEARER-SENTINEL-7A";
    private const string StripeSentinel = "OBS-STRIPE-SIGNATURE-SENTINEL-7A";
    private const string PayloadSentinel = "OBS-STRIPE-PAYLOAD-SENTINEL-7A";
    private const string ModerationSentinel = "OBS-MODERATION-SCORE-SENTINEL-7A";
    private const string TrainerSentinel = "7a7a7a7a-0000-4000-8000-00000000007a";
    private const string ThrowPath = "/__test/observability/throw";

    private static readonly string[] Sentinels =
    [
        PasswordSentinel, EmailSentinel, TokenSentinel, CookieSentinel, BearerSentinel,
        StripeSentinel, PayloadSentinel, ModerationSentinel, TrainerSentinel
    ];

    private readonly PostgresApiFixture _database;
    private readonly List<Activity> _spans = [];
    private readonly CapturingLoggerProvider _logs = new();
    private readonly RecordingSentryTransport _sentry = new();
    private ApiWebApplicationFactory _factory = null!;

    public ObservabilityTelemetryTests(PostgresApiFixture database) => _database = database;

    public ValueTask InitializeAsync()
    {
        _factory = new ApiWebApplicationFactory(_database.ConnectionString)
        {
            AdditionalSettings = new Dictionary<string, string>
            {
                ["Observability:Enabled"] = "true",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://127.0.0.1:9",
                ["Sentry:Enabled"] = "true",
                ["Sentry:Dsn"] = "https://public@127.0.0.1:9/1",
            },
            ConfigureServices = services =>
            {
                services.AddSingleton<ILoggerProvider>(_logs);
                services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(_spans));
                services.AddSingleton<IStartupFilter, ThrowingEndpointStartupFilter>();
                services.Configure<SentryAspNetCoreOptions>(options => options.Transport = _sentry);
            }
        };

        // O fluxo do teste segue até ao pipeline: o MetricCapture só conta o que é deste teste.
        _factory.Server.PreserveExecutionContext = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Login_ExportsServerAndSqlSpans_WithoutCredentialsCookiesOrQueryTokens()
    {
        var client = _factory.CreateOriginClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/auth/login?token={TokenSentinel}")
        {
            Content = JsonBody($$"""{"email":"{{EmailSentinel}}","password":"{{PasswordSentinel}}"}""")
        };
        request.Headers.Add("Cookie", $"pt_refresh={CookieSentinel}");
        request.Headers.Add("Authorization", $"Bearer {BearerSentinel}");
        request.Headers.Add("User-Agent", "ptm-observability-test");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // O exporter OTLP está em baixo e o login continua a responder como sempre.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var correlationId = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));

        var server = await WaitForSpanAsync(span =>
            span.Kind == ActivityKind.Server &&
            Equals(span.GetTagItem("http.route"), "api/v1/auth/login"));
        Assert.Equal(correlationId, server.GetTagItem("ptm.correlation_id"));
        // S7-11: sem path com ids, query nem user-agent; o template da rota chega.
        foreach (var attribute in new[] { "url.path", "url.query", "user_agent.original" })
            Assert.Null(server.GetTagItem(attribute));

        var sql = SpansSnapshot().Where(span =>
            span.Source.Name == "Npgsql" && span.TraceId == server.TraceId).ToArray();
        Assert.NotEmpty(sql);

        AssertNoSentinel(SpansSnapshot());
        AssertNoSentinel(_logs.Snapshot());
    }

    [Fact]
    public async Task HealthChecks_AreNotTraced()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        await client.GetAsync("/api/v1/auth/account", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await WaitForSpanAsync(span =>
            span.Kind == ActivityKind.Server &&
            Equals(span.GetTagItem("http.route"), "api/v1/auth/account"));
        Assert.DoesNotContain(SpansSnapshot(), span =>
            span.Kind == ActivityKind.Server &&
            span.GetTagItem("http.route") is string route &&
            route.Contains("health", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OutgoingHttpSpans_KeepOnlyTheProviderHost()
    {
        // Os URLs do R2 levam ids no caminho e a assinatura na query. A porta 9 recusa a
        // ligação, mas a span do HttpClient nativo fica criada.
        _ = _factory.Services;
        using var http = new HttpClient();
        try
        {
            await http.GetAsync(
                $"http://127.0.0.1:9/videos/clip.mp4?X-Amz-Signature={TokenSentinel}",
                TestContext.Current.CancellationToken);
        }
        catch (HttpRequestException)
        {
        }

        var span = await WaitForSpanAsync(candidate =>
            candidate.Kind == ActivityKind.Client &&
            candidate.Source.Name == "System.Net.Http" &&
            Equals(candidate.GetTagItem("server.port"), 9));
        Assert.Equal("127.0.0.1", span.GetTagItem("server.address"));
        Assert.Null(span.GetTagItem("url.full"));
        AssertNoSentinel([span]);
    }

    [Fact]
    public async Task InboundTraceparent_IsIgnored()
    {
        // Um cliente anónimo não escolhe o trace id nem força a amostragem.
        const string forgedTraceId = "7a7a7a7a7a7a7a7a7a7a7a7a7a7a7a7a";
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/account");
        request.Headers.Add("traceparent", $"00-{forgedTraceId}-1111111111111111-01");
        request.Headers.Add("baggage", $"token={TokenSentinel}");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var correlationId = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
        var server = await WaitForSpanAsync(span =>
            span.Kind == ActivityKind.Server &&
            Equals(span.GetTagItem("ptm.correlation_id"), correlationId));
        Assert.NotEqual(forgedTraceId, server.TraceId.ToHexString());
        Assert.False(server.HasRemoteParent);
        Assert.Empty(server.Baggage);
    }

    [Fact]
    public async Task UnhandledException_ReachesSentryWithoutHeadersCookiesQueryOrBody()
    {
        var client = _factory.CreateOriginClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ThrowPath}?token={TokenSentinel}")
        {
            Content = JsonBody($$"""{"password":"{{PasswordSentinel}}","payload":"{{PayloadSentinel}}"}""")
        };
        request.Headers.Add("Cookie", $"pt_refresh={CookieSentinel}");
        request.Headers.Add("Authorization", $"Bearer {BearerSentinel}");
        request.Headers.Add("Stripe-Signature", StripeSentinel);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await _factory.Services.GetRequiredService<IHub>().FlushAsync(TimeSpan.FromSeconds(5));

        var envelopes = _sentry.Envelopes;
        var exceptionEvent = Assert.Single(envelopes, item => item.Contains(nameof(ObservabilityTestException)));
        Assert.Contains(ThrowPath, exceptionEvent, StringComparison.Ordinal);
        Assert.Contains(envelopes, item => item.Contains("test_failure", StringComparison.Ordinal));
        foreach (var envelope in envelopes)
        {
            foreach (var sentinel in Sentinels)
                Assert.DoesNotContain(sentinel, envelope, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task SentryUnreachable_DoesNotBreakRequests()
    {
        // Sem o transport de teste: o SDK tenta mesmo enviar para a porta 9.
        using var factory = new ApiWebApplicationFactory(_database.ConnectionString)
        {
            AdditionalSettings = new Dictionary<string, string>
            {
                ["Sentry:Enabled"] = "true",
                ["Sentry:Dsn"] = "https://public@127.0.0.1:9/1",
            },
            ConfigureServices = services =>
                services.AddSingleton<IStartupFilter, ThrowingEndpointStartupFilter>()
        };
        var client = factory.CreateOriginClient();

        var failed = await client.PostAsync(ThrowPath, JsonBody("{}"), TestContext.Current.CancellationToken);
        await factory.Services.GetRequiredService<IHub>().FlushAsync(TimeSpan.FromSeconds(2));
        var next = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    }

    [Fact]
    public async Task ReadRequest_RecordsDatabaseCommandsPerRouteTemplate()
    {
        using var capture = new MetricCapture(PtManagerTelemetry.Name, "ptm.http.db_commands");
        var client = _factory.CreateOriginClient();

        var response = await client.PostAsync(
            "/api/v1/auth/login",
            JsonBody($$"""{"email":"{{EmailSentinel}}","password":"{{PasswordSentinel}}"}"""),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var measurement = Assert.Single(capture.Measurements, m =>
            Equals(m.Tags["http.route"], "api/v1/auth/login"));
        Assert.True(measurement.Value > 0);
        Assert.Equal("POST", measurement.Tags["http.request.method"]);
    }

    [Fact]
    public async Task RejectedStripeWebhook_IsCountedWithoutEventData()
    {
        using var capture = new MetricCapture(PtManagerTelemetry.Name, "ptm.stripe.webhook");
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/v1/billing/webhook",
            JsonBody($$"""{"id":"{{PayloadSentinel}}"}"""),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var measurement = Assert.Single(capture.Measurements);
        Assert.Equal(1, measurement.Value);
        Assert.Equal("rejected", Assert.Single(measurement.Tags).Value);
    }

    private static StringContent JsonBody(string json) => new(json, Encoding.UTF8, "application/json");

    private Activity[] SpansSnapshot()
    {
        lock (_spans)
            return [.. _spans];
    }

    private async Task<Activity> WaitForSpanAsync(Func<Activity, bool> predicate)
    {
        // O exporter em memória é síncrono, mas a span do servidor fecha depois de a resposta
        // chegar ao cliente. Sem ForceFlush: com o OTLP em baixo, cada flush bloquearia.
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var match = SpansSnapshot().FirstOrDefault(predicate);
            if (match is not null)
                return match;

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        throw new Xunit.Sdk.XunitException("Expected span was not exported.");
    }

    private static void AssertNoSentinel(IEnumerable<Activity> spans)
    {
        foreach (var span in spans)
        {
            var text = string.Join('|', span.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"))
                + span.DisplayName + span.StatusDescription
                + string.Join('|', span.Events.SelectMany(e => e.Tags.Select(tag => $"{tag.Key}={tag.Value}")));

            foreach (var sentinel in Sentinels)
                Assert.DoesNotContain(sentinel, text, StringComparison.Ordinal);
        }
    }

    private static void AssertNoSentinel(IEnumerable<CapturedLogEntry> entries)
    {
        foreach (var entry in entries)
        {
            var text = entry.FormattedMessage
                + string.Join('|', entry.State.Select(pair => $"{pair.Key}={pair.Value}"))
                + entry.Exception;

            foreach (var sentinel in Sentinels)
                Assert.DoesNotContain(sentinel, text, StringComparison.Ordinal);
        }
    }

    private sealed class ObservabilityTestException(string message) : Exception(message);

    /// <summary>Acrescenta, depois de todo o pipeline real, um endpoint que lança.</summary>
    /// <remarks>
    /// Fica a jusante do <c>ExceptionHandlingMiddleware</c>, como um controller: a exceção
    /// percorre o caminho real (LogError → integração de logging do Sentry → BeforeSend).
    /// </remarks>
    private sealed class ThrowingEndpointStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Run(context =>
            {
                if (context.Request.Path != ThrowPath)
                    return Task.CompletedTask;

                // Como o dispatcher num dead letter: LogError dentro de um scope com TrainerId.
                var logger = context.RequestServices.GetRequiredService<ILogger<ObservabilityTelemetryTests>>();
                using (logger.BeginScope(new Dictionary<string, object> { ["TrainerId"] = Guid.Parse(TrainerSentinel) }))
                    logger.LogError("Durable job moved to dead letter with failure code {FailureCode}.", "test_failure");

                throw new ObservabilityTestException("Unexpected failure for the observability test.");
            });
        };
    }

    /// <summary>Transport do Sentry que serializa cada envelope em vez de o enviar.</summary>
    private sealed class RecordingSentryTransport : ITransport
    {
        private readonly List<string> _envelopes = [];

        public IReadOnlyList<string> Envelopes
        {
            get
            {
                lock (_envelopes)
                    return [.. _envelopes];
            }
        }

        public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream();
            await envelope.SerializeAsync(stream, null, cancellationToken);
            lock (_envelopes)
                _envelopes.Add(Encoding.UTF8.GetString(stream.ToArray()));
        }
    }
}
