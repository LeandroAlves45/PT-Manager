using Application.Features.Jobs.Dispatching;
using Application.Features.Training.ExerciseVideos.CompleteExerciseVideoUpload;
using Application.Features.Training.ExerciseVideos.GetExerciseVideoPlayback;
using Application.Features.Training.ExerciseVideos.GetExerciseVideoUpload;
using Application.Features.Training.ExerciseVideos.Processing;
using Application.Features.Training.ExerciseVideos.RemoveExerciseVideo;
using Application.Features.Training.ExerciseVideos.RequestExerciseVideoUpload;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Features.Training.ExerciseVideos;

/// <summary>Regista os casos de uso e os Durable Jobs dos vídeos geridos.</summary>
public static class ExerciseVideoApplicationExtensions
{
    public static IServiceCollection AddExerciseVideoApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<RequestExerciseVideoUploadHandler>();
        services.AddScoped<CompleteExerciseVideoUploadHandler>();
        services.AddScoped<GetExerciseVideoUploadHandler>();
        services.AddScoped<RemoveExerciseVideoHandler>();
        services.AddScoped<GetExerciseVideoPlaybackHandler>();

        services.AddScoped<
            IValidator<RequestExerciseVideoUploadCommand>,
            RequestExerciseVideoUploadCommandValidator>();

        services.AddScoped<IDurableJobHandler, ProcessExerciseVideoJobHandler>();
        services.AddScoped<IDurableJobHandler, ExpireExerciseVideoUploadJobHandler>();
        services.AddScoped<IDurableJobHandler, DeleteExerciseVideoObjectJobHandler>();

        return services;
    }
}
