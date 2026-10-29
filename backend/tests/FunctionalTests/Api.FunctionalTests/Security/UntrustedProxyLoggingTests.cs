using System.Net;
using Api.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Api.FunctionalTests.Security;

/// <summary>
/// O aviso de proxy não confiável corre à frente do <see cref="ForwardedHeadersMiddleware"/>
/// real, como no <c>Program.cs</c>: estes testes provam o par em conjunto.
/// </summary>
public sealed class UntrustedProxyLoggingTests
{
    private static readonly IPAddress RenderProxy = IPAddress.Parse("10.20.30.40");

    [Fact]
    public async Task UntrustedProxy_IsReportedOnceWithItsAddress()
    {
        var logger = new RecordingLogger();
        var pipeline = Pipeline(knownNetwork: "192.0.2.1/32", logger);

        await pipeline(Request(RenderProxy, "203.0.113.9"));
        await pipeline(Request(RenderProxy, "203.0.113.10"));

        var entry = Assert.Single(logger.Warnings);
        Assert.Contains("10.20.30.40", entry, StringComparison.Ordinal);
        Assert.DoesNotContain("203.0.113", entry, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TrustedProxy_IsNotReportedAndClientAddressIsApplied()
    {
        var logger = new RecordingLogger();
        var pipeline = Pipeline(knownNetwork: "10.0.0.0/8", logger);
        var context = Request(RenderProxy, "198.51.100.7, 203.0.113.9");

        await pipeline(context);

        Assert.Empty(logger.Warnings);
        Assert.Equal(IPAddress.Parse("203.0.113.9"), context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task ForgedOriginalForHeader_DoesNotHideAnUntrustedProxy()
    {
        var logger = new RecordingLogger();
        var pipeline = Pipeline(knownNetwork: "192.0.2.1/32", logger);
        var context = Request(RenderProxy, "203.0.113.9");
        context.Request.Headers[ForwardedHeadersDefaults.XOriginalForHeaderName] = "1.2.3.4";

        await pipeline(context);

        Assert.Contains("10.20.30.40", Assert.Single(logger.Warnings), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TrustedChainWithSpoofedEntry_NeverLogsAClientAddress()
    {
        var logger = new RecordingLogger();
        var pipeline = Pipeline(knownNetwork: "10.0.0.0/8", logger);

        await pipeline(Request(RenderProxy, "198.51.100.7, 203.0.113.9"));

        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public async Task RequestWithoutForwardedFor_IsNotReported()
    {
        var logger = new RecordingLogger();
        var pipeline = Pipeline(knownNetwork: "192.0.2.1/32", logger);

        await pipeline(Request(RenderProxy, forwardedFor: null));

        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public async Task ForwardedHeadersDisabled_NeverReports()
    {
        var logger = new RecordingLogger();
        var pipeline = Pipeline(knownNetwork: null, logger);

        await pipeline(Request(RenderProxy, "203.0.113.9"));

        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public async Task ManyDistinctProxies_AreCappedToAvoidFloodingLogs()
    {
        var logger = new RecordingLogger();
        var pipeline = Pipeline(knownNetwork: "192.0.2.1/32", logger);

        for (var index = 1; index <= UntrustedProxyLoggingMiddleware.MaxReportedProxies + 10; index++)
            await pipeline(Request(IPAddress.Parse($"10.0.1.{index}"), "203.0.113.9"));

        Assert.Equal(UntrustedProxyLoggingMiddleware.MaxReportedProxies, logger.Warnings.Count);
    }

    /// <summary>Mesma configuração que <c>ApiForwardedHeaders</c> aplica em Production.</summary>
    private static RequestDelegate Pipeline(string? knownNetwork, ILogger<UntrustedProxyLoggingMiddleware> logger)
    {
        var options = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.None };
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        if (knownNetwork is not null)
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(knownNetwork));
            options.ForwardLimit = 1;
        }

        // Mesma ordem que o Program.cs: o aviso corre antes do ForwardedHeadersMiddleware.
        var wrapped = Options.Create(options);
        var forwarded = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, wrapped);
        var logging = new UntrustedProxyLoggingMiddleware(forwarded.Invoke, wrapped, logger);
        return logging.InvokeAsync;
    }

    private static DefaultHttpContext Request(IPAddress peer, string? forwardedFor)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = peer;
        if (forwardedFor is not null)
            context.Request.Headers[ForwardedHeadersDefaults.XForwardedForHeaderName] = forwardedFor;
        return context;
    }

    private sealed class RecordingLogger : ILogger<UntrustedProxyLoggingMiddleware>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
                Warnings.Add(formatter(state, exception));
        }
    }
}
