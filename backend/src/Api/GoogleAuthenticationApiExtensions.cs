using Api.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Api;

/// <summary>Compõe apenas os serviços da fronteira HTTP Google.</summary>
public static class GoogleAuthenticationApiExtensions
{
    public static IServiceCollection AddGoogleAuthenticationApi(
        this IServiceCollection services)
    {
        services.AddSingleton<GoogleChallengeCookieWriter>();
        return services;
    }
}
