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

        // Em Production a lista vazia é um erro de deployment, não um
        // default aceitável. Com a lista vazia o middleware ignora os headers e
        // o rate limiting por IP fica silenciosamente inútil.
        if (environment.IsProduction() &&
            knownProxies.Length == 0 &&
            knownNetworks.Length == 0)
            throw new InvalidOperationException(
                "Configuration 'ForwardedHeaders' must declare at least one known " +
                "proxy or network in production.");

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

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
