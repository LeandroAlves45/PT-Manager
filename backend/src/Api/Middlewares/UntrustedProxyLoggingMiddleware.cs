using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Api.Middlewares;

/// <summary>
/// Avisa quando um pedido traz <c>X-Forwarded-For</c> mas a ligação veio de um IP fora de
/// <c>ForwardedHeaders:KnownNetworks/KnownProxies</c>.
/// </summary>
/// <remarks>
/// A Render não publica os IPs dos proxies de entrada. Sem este aviso, um CIDR errado só
/// se nota porque todos os utilizadores partilham a partição de rate limit do proxy. O log
/// mostra o IP a declarar. Corre imediatamente antes de <c>UseForwardedHeaders</c>, quando o
/// IP da ligação ainda é o do proxy, e compara-o com a configuração. Nenhum cabeçalho enviado
/// pelo cliente muda esse IP, e o IP do cliente nunca entra no log.
/// </remarks>
public sealed class UntrustedProxyLoggingMiddleware
{
    /// <summary>Máximo de IPs distintos avisados por processo.</summary>
    public const int MaxReportedProxies = 32;

    private readonly RequestDelegate _next;
    private readonly ILogger<UntrustedProxyLoggingMiddleware> _logger;
    private readonly ForwardedHeadersOptions _options;
    private readonly bool _enabled;
    private readonly ConcurrentDictionary<string, byte> _reported = new(StringComparer.Ordinal);

    public UntrustedProxyLoggingMiddleware(
        RequestDelegate next,
        IOptions<ForwardedHeadersOptions> options,
        ILogger<UntrustedProxyLoggingMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options.Value;
        // Sem proxies configurados (Development) os cabeçalhos são ignorados de propósito.
        _enabled = _options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedFor);
    }

    public Task InvokeAsync(HttpContext httpContext)
    {
        if (_enabled)
            ReportIfUntrusted(httpContext);

        return _next(httpContext);
    }

    private void ReportIfUntrusted(HttpContext httpContext)
    {
        if (!httpContext.Request.Headers.ContainsKey(ForwardedHeadersDefaults.XForwardedForHeaderName))
            return;

        var peer = httpContext.Connection.RemoteIpAddress;
        if (peer is null || IsTrusted(peer))
            return;

        var proxy = peer.ToString();
        if (_reported.Count >= MaxReportedProxies || !_reported.TryAdd(proxy, 0))
            return;

        _logger.LogWarning(
            "X-Forwarded-For ignored: the request came from proxy {ProxyAddress}, which is not in " +
            "ForwardedHeaders:KnownNetworks or KnownProxies. Declare its network to see client IPs.",
            proxy);
    }

    private bool IsTrusted(IPAddress address)
    {
        var candidate = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return _options.KnownProxies.Any(known => known.Equals(candidate)) ||
            _options.KnownIPNetworks.Any(network => network.Contains(candidate));
    }
}
