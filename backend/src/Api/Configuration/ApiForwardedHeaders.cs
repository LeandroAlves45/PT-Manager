using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Api.Configuration;

/// <summary>Configura a confiança em proxies conhecidos.</summary>
public static class ApiForwardedHeaders
{
    private const string SectionName = "ForwardedHeaders";

    /// <summary>Regista as opções a partir da configuração explícita.</summary>
    public static IServiceCollection AddApiForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var section = configuration.GetSection(SectionName);
        var knownProxies = section.GetSection("KnownProxies").Get<string[]>() ?? [];
        var knownNetworks = section.GetSection("KnownNetworks").Get<string[]>() ?? [];

        var hasTrustedProxies = knownProxies.Length > 0 || knownNetworks.Length > 0;

        // Listas vazias NÃO fazem o middleware ignorar os headers: com KnownProxies
        // e KnownIPNetworks limpos, X-Forwarded-* é aceite de qualquer origem
        // (breaking change do ASP.NET Core 8). Sem proxies confiáveis o
        // processamento é desligado; em Production é erro de deployment.
        if (!hasTrustedProxies && environment.IsProduction())
            throw new InvalidOperationException(
                "Configuration 'ForwardedHeaders' must declare at least one known " +
                "proxy or network in production.");

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            if (!hasTrustedProxies)
            {
                options.ForwardedHeaders = ForwardedHeaders.None;
                return;
            }

            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            foreach (var proxy in knownProxies)
                options.KnownProxies.Add(IPAddress.Parse(proxy));

            foreach (var network in knownNetworks)
            {
                if (!System.Net.IPNetwork.TryParse(network, out var parsed))
                    throw new InvalidOperationException(
                        $"Configuration 'ForwardedHeaders:KnownNetworks' entry '{network}' is invalid.");

                options.KnownIPNetworks.Add(parsed);
            }

            options.ForwardLimit = 1;
        });

        return services;
    }
}
