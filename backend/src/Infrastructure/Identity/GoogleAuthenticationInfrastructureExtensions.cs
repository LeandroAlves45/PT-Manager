using Application.Features.Authentication.Google.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Identity;

/// <summary>Compõe apenas os adapters Google de autenticação externa.</summary>
public static class GoogleAuthenticationInfrastructureExtensions
{
    public static IServiceCollection AddGoogleAuthenticationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<GoogleOptions>()
            .Bind(configuration.GetSection(GoogleOptions.SectionName))
            .Validate(options => options.IsValid(),
                "Configuration section 'Google' is missing or invalid.")
            .ValidateOnStart();

        services.AddSingleton<IGoogleIdTokenValidator, GoogleIdTokenValidator>();
        services.AddScoped<IExternalIdentityVerifier, GoogleExternalIdentityVerifier>();
        services.AddScoped<ExternalAuthenticationStore>();
        services.AddScoped<IExternalChallengeStore>(provider =>
            provider.GetRequiredService<ExternalAuthenticationStore>());
        services.AddScoped<IExternalAuthenticationStore>(provider =>
            provider.GetRequiredService<ExternalAuthenticationStore>());

        return services;
    }
}
