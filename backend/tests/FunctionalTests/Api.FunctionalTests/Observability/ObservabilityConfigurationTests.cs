using Api.FunctionalTests.Support;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Sentry;

namespace Api.FunctionalTests.Observability;

/// <summary>
/// A telemetria nasce desligada, e uma integração ligada sem destino válido não arranca.
/// </summary>
/// <remarks>
/// O host arranca sem tocar na base de dados: a connection string não é usada.
/// </remarks>
public sealed class ObservabilityConfigurationTests
{
    private const string UnusedConnectionString =
        "Host=localhost;Port=5432;Database=unused;Username=unused;Password=unused";

    private const string SecretHeaderValue = "Authorization=Basic OBS-OTLP-TOKEN-SENTINEL-7A";

    [Fact]
    public void ByDefault_NoExporterAndNoSentryAreRegistered()
    {
        using var factory = new ApiWebApplicationFactory(UnusedConnectionString);

        Assert.Null(factory.Services.GetService<TracerProvider>());
        Assert.Null(factory.Services.GetService<IHub>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-uri")]
    // O header OTLP leva o token: http só se aceita em loopback.
    [InlineData("http://otlp-gateway-prod-eu-west-2.grafana.net/otlp")]
    public void ObservabilityEnabled_WithoutAllowedEndpoint_FailsAtStartupWithoutEchoingSecrets(
        string? endpoint)
    {
        var settings = new Dictionary<string, string>
        {
            ["Observability:Enabled"] = "true",
            ["OTEL_EXPORTER_OTLP_HEADERS"] = SecretHeaderValue,
        };
        if (endpoint is not null)
            settings["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint;

        using var factory = new ApiWebApplicationFactory(UnusedConnectionString)
        {
            AdditionalSettings = settings
        };

        var exception = Assert.ThrowsAny<Exception>(() => factory.Services);
        var message = FlattenMessages(exception);
        Assert.Contains("OTEL_EXPORTER_OTLP_ENDPOINT", message, StringComparison.Ordinal);
        Assert.DoesNotContain("OBS-OTLP-TOKEN-SENTINEL-7A", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("-0.1")]
    public void ObservabilityEnabled_WithSamplingRatioOutOfRange_FailsAtStartup(string ratio)
    {
        using var factory = new ApiWebApplicationFactory(UnusedConnectionString)
        {
            AdditionalSettings = new Dictionary<string, string>
            {
                ["Observability:Enabled"] = "true",
                ["Observability:SamplingRatio"] = ratio,
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "https://otlp.example.test/otlp",
            }
        };

        var exception = Assert.ThrowsAny<Exception>(() => factory.Services);
        Assert.Contains("Observability:SamplingRatio", FlattenMessages(exception), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("http://public@sentry.example.test/1")]
    public void SentryEnabled_WithoutHttpsDsn_FailsAtStartup(string? dsn)
    {
        var settings = new Dictionary<string, string> { ["Sentry:Enabled"] = "true" };
        if (dsn is not null)
            settings["Sentry:Dsn"] = dsn;

        using var factory = new ApiWebApplicationFactory(UnusedConnectionString)
        {
            AdditionalSettings = settings
        };

        var exception = Assert.ThrowsAny<Exception>(() => factory.Services);
        var message = FlattenMessages(exception);
        Assert.Contains("Sentry:Dsn", message, StringComparison.Ordinal);
        Assert.DoesNotContain("public@", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ObservabilityEnabled_WithHttpsEndpoint_RegistersTheTracerProvider()
    {
        using var factory = new ApiWebApplicationFactory(UnusedConnectionString)
        {
            AdditionalSettings = new Dictionary<string, string>
            {
                ["Observability:Enabled"] = "true",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "https://127.0.0.1:9/otlp",
            }
        };

        Assert.NotNull(factory.Services.GetService<TracerProvider>());
    }

    private static string FlattenMessages(Exception exception)
    {
        var messages = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
            messages.Add(current.Message);

        return string.Join(" | ", messages);
    }
}
