using System.Diagnostics;
using System.Runtime.CompilerServices;
using Application.Common.Observability;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Sentry.AspNetCore;
using Sentry.Extensibility;

namespace Api.Configuration;

/// <summary>Opções da secção <c>Observability</c> (OpenTelemetry).</summary>
/// <remarks>
/// O destino e a autenticação do exporter vêm das variáveis standard
/// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> e <c>OTEL_EXPORTER_OTLP_HEADERS</c>, que o tile
/// OpenTelemetry do Grafana Cloud dá já prontas. O header leva o token: fica fora do
/// <c>appsettings</c> e nunca passa por esta classe.
/// </remarks>
public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>Desligado por omissão: o CI, testes e o dev arrancam sem exporter.</summary>
    public bool Enabled { get; init; }

    /// <summary>Fração dos traces raiz guardados (0 a 1). Com tráfego do MVP.</summary>
    public double SamplingRatio { get; init; } = 1.0;
}

/// <summary>Opções da secção <c>Sentry</c> que o PT Manager lê antes do SDK.</summary>
public sealed class SentryReportingOptions
{
    public const string SectionName = "Sentry";

    /// <summary>Desligado por omissão, como as outras integrações externas.</summary>
    public bool Enabled { get; init; }

    /// <summary>DNS do projeto Sentry (organização na região EU).</summary>
    public string? Dsn { get; init; }
}

/// <summary>Logs, traces, métricas e erros da API.</summary>
public static class ApiObservability
{
    /// <summary>Nome do serviço no Grafana e no Sentry.</summary>
    public const string ServiceName = "pt-manager-api";

    /// <summary>Endpoint OTLP, lido pelo exporter a partir das configurações.</summary>
    public const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>
    /// Meters escutados: os próprios (<see cref="PtManagerTelemetry"/>) e os nativos do .NET,
    /// que dispensam pacotes de instrumentação.
    /// </summary>
    private static readonly string[] Meters =
    [
        PtManagerTelemetry.Name,
        // http.server.request.duration por http.route: latência e erros 5xx.
        "Microsoft.AspNetCore.Hosting",
        // aspnetcore.rate_limiting.requests por policy: saturação do rate limiting local.
        "Microsoft.AspNetCore.RateLimiting",
        // http.client.request.duration por server.address: Resend, Stripe, Cloudinary, Vision, R2.
        "System.Net.Http",
        // db.client.operation.duration e db.client.connection.*: pressão sobre o PostgreSQL.
        "Npgsql",
        "System.Runtime",
    ];

    /// <summary>
    /// Fontes de spans: as próprias, o Npgsql (uma span por comando SQL, com o texto
    /// parametrizado e sem valores) e o HttpClient nativo do .NET 10.
    /// </summary>
    private static readonly string[] ActivitySources =
    [
        PtManagerTelemetry.Name,
        "Npgsql",
        "System.Net.Http",
    ];

    /// <summary>Configura a observabilidade a partir da configuração do host.</summary>
    /// <exception cref="InvalidOperationException">
    /// Uma integração ligada sem destino válido. Falha no arranque, como o
    /// <c>ValidateOnStart</c> das outras opções, e nunca escreve o valor recebido.
    /// </exception>
    public static WebApplicationBuilder AddApiObservability(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Com os scopes, cada linha JSON leva CorrelationId, TraceId e SpanId (e JobId no
        // dispatcher). O Render só guarda stdout: é aqui que se cruza um log com um trace.
        builder.Logging.Configure(options =>
            options.ActivityTrackingOptions =
                ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);

        // Sempre, com ou sem exporter: o trace id de cada pedido nasce aqui e não no cliente.
        builder.Services.AddSingleton<DistributedContextPropagator>(
            new InboundTraceContextIgnoringPropagator(DistributedContextPropagator.CreateDefaultPropagator()));

        AddOpenTelemetry(builder);
        AddSentry(builder);
        return builder;
    }

    private static void AddOpenTelemetry(WebApplicationBuilder builder)
    {
        var options = builder.Configuration.GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

        if (!options.Enabled)
            return;

        if (options.SamplingRatio is < 0 or > 1)
            throw new InvalidOperationException(
                "Configuration 'Observability:SamplingRatio' must be between 0 and 1.");

        if (!IsAllowedOtlpEndpoint(builder.Configuration[OtlpEndpointKey]))
            throw new InvalidOperationException(
                $"Configuration '{OtlpEndpointKey}' must be an absolute https URI (http only on loopback) when Observability is enabled.");

        // Global ao processo: a instrumentação de ASP.NET Core extrai com este propagator.
        Sdk.SetDefaultTextMapPropagator(new InboundTraceContextIgnoringTextMapPropagator());

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(ServiceName)
                .AddAttributes(
                [
                    new KeyValuePair<string, object>(
                        "deployment.environment.name",
                        builder.Environment.EnvironmentName)
                ]))
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(options.SamplingRatio)))
                .AddSource(ActivitySources)
                .AddProcessor(new SensitiveSpanAttributesProcessor())
                .AddAspNetCoreInstrumentation(instrumentation =>
                    // Os health checks não são tráfego de negócio e gastaria quota.
                    instrumentation.Filter = context =>
                        !context.Request.Path.StartsWithSegments("/health"))
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddMeter(Meters)
                .AddOtlpExporter());
    }

    private static void AddSentry(WebApplicationBuilder builder)
    {
        var options = builder.Configuration.GetSection(SentryReportingOptions.SectionName)
            .Get<SentryReportingOptions>() ?? new SentryReportingOptions();

        if (!options.Enabled)
            return;

        if (!Uri.TryCreate(options.Dsn, UriKind.Absolute, out var dsn) || dsn.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException(
                "Configuration 'Sentry:Dsn' must be an absolute https URI when Sentry is enabled.");

        builder.WebHost.UseSentry(sentry => ConfigureSentry(
            sentry,
            options.Dsn!,
            builder.Environment,
            // O Render expõe o commit do deploy; fora dele o SDK usa a versão do assembly.
            builder.Configuration["RENDER_GIT_COMMIT"]));
    }

    /// <summary>Política de dados do Sentry: só erros, sem PII, sem corpo, sem headers.</summary>
    internal static void ConfigureSentry(
        SentryAspNetCoreOptions sentry,
        string dsn,
        IHostEnvironment environment,
        string? release)
    {
        sentry.Dsn = dsn;
        sentry.Environment = environment.EnvironmentName;
        if (!string.IsNullOrWhiteSpace(release))
            sentry.Release = release;

        // Só erros. O ExceptionHandlingMiddleware apanha tudo e escreve LogError: é a
        // integração de logging que transforma esse log num evento.
        sentry.MinimumEventLevel = LogLevel.Error;
        sentry.TracesSampleRate = 0;

        // Nunca IP, cookies, utilizador nem corpo (o payload Stripe incluído).
        sentry.SendDefaultPii = false;
        sentry.MaxRequestBodySize = RequestSize.None;

        // As chamadas HTTP já ficam nas spans do OpenTelemetry; o handler do Sentry
        // juntaria breadcrumbs com URLs completos (os pré-assinados do R2 levam a assinatura).
        sentry.DisableSentryHttpMessageHandler = true;

        sentry.SetBeforeSend(static (sentryEvent, _) => SentryEventScrubber.Scrub(sentryEvent));
        sentry.SetBeforeBreadcrumb(static (breadcrumb, _) => SentryEventScrubber.ScrubBreadcrumb(breadcrumb));
    }

    // O header OTLP leva o token do Grafana: http só para um collector local (loopback).
    private static bool IsAllowedOtlpEndpoint(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));
}
