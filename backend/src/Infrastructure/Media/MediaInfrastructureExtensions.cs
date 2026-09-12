using Application.Common.Abstractions;
using Infrastructure.Media.Cloudinary;
using Infrastructure.Media.Imaging;
using Infrastructure.Media.Moderation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.Media;

/// <summary>Compõe os adapters de imagens geridas.</summary>
internal static class MediaInfrastructureExtensions
{
    internal static IServiceCollection AddMediaInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Sem estado: descodifica e reencoda por pedido.
        services.AddSingleton<IImageProcessor, SkiaImageProcessor>();

        services.AddOptions<CloudinaryOptions>()
            .Bind(configuration.GetSection(CloudinaryOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<CloudinaryOptions>, CloudinaryOptionsValidator>();

        services.AddHttpClient<IMediaStorage, CloudinaryMediaStorage>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<CloudinaryOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = options.Timeout;
        });

        services.AddOptions<VisionOptions>()
            .Bind(configuration.GetSection(VisionOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<VisionOptions>, VisionOptionsValidator>();

        // Singleton: a credencial guarda e renova o token em memória.
        services.AddSingleton<IVisionAccessTokenProvider, VisionAccessTokenProvider>();

        services.AddHttpClient<IImageModerationService, VisionSafeSearchModerationService>(
            (provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<VisionOptions>>().Value;
                client.BaseAddress = options.BaseAddress;
                client.Timeout = options.Timeout;
            });

        return services;
    }
}
