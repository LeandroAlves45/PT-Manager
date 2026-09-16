using Application.Features.Training.ExerciseVideos;
using Application.Features.Training.ExerciseVideos.Abstractions;
using Infrastructure.Persistence.Training;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.Media.Video;

/// <summary>Compõe o storage R2, o probe de container e a persistência dos vídeos geridos.</summary>
internal static class VideoInfrastructureExtensions
{
    internal static IServiceCollection AddVideoInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<R2Options>()
            .Bind(configuration.GetSection(R2Options.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<R2Options>, R2OptionsValidator>();

        services.AddOptions<ExerciseVideoOptions>()
            .Bind(configuration.GetSection(ExerciseVideoOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<ExerciseVideoOptions>, ExerciseVideoOptionsValidator>();

        services.AddSingleton<ExerciseVideoSettings>(provider =>
            provider.GetRequiredService<IOptions<ExerciseVideoOptions>>().Value.ToSettings());

        // Singletons: o cliente S3 reutiliza ligações e o storage não tem estado por pedido.
        services.AddSingleton<R2ClientProvider>();
        services.AddSingleton<R2VideoObjectStorage>();
        services.AddSingleton<IVideoObjectStorage>(provider =>
            provider.GetRequiredService<R2VideoObjectStorage>());
        services.AddSingleton<IVideoObjectRangeReader>(provider =>
            provider.GetRequiredService<R2VideoObjectStorage>());
        services.AddSingleton<IVideoMetadataProbe, VideoMetadataProbe>();

        services.AddScoped<IExerciseVideoStore, ExerciseVideoStore>();
        services.AddScoped<IExerciseVideoQueries, ExerciseVideoQueries>();
        services.AddScoped<IExerciseVideoProcessingStore, ExerciseVideoProcessingStore>();

        return services;
    }
}
