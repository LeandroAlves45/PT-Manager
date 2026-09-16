using Application.Features.Training.ExerciseVideos;
using Microsoft.Extensions.Options;

namespace Infrastructure.Media.Video;

/// <summary>Configuração operacional dos vídeos geridos.</summary>
public sealed class ExerciseVideoOptions
{
    public const string SectionName = "ExerciseVideos";

    public int MaxVideosPerTrainer { get; init; } = 20;
    public TimeSpan UploadUrlLifetime { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan PlaybackUrlLifetime { get; init; } = TimeSpan.FromMinutes(30);
    public TimeSpan AbandonmentGrace { get; init; } = TimeSpan.FromHours(1);

    internal ExerciseVideoSettings ToSettings() => new(
        MaxVideosPerTrainer,
        UploadUrlLifetime,
        PlaybackUrlLifetime,
        AbandonmentGrace);
}

/// <summary>Falha no arranque com limites fora dos intervalos aceites.</summary>
internal sealed class ExerciseVideoOptionsValidator : IValidateOptions<ExerciseVideoOptions>
{
    public ValidateOptionsResult Validate(string? name, ExerciseVideoOptions options)
    {
        try
        {
            _ = options.ToSettings();
            return ValidateOptionsResult.Success;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return ValidateOptionsResult.Fail(
                $"ExerciseVideos option '{exception.ParamName}' is outside the allowed range.");
        }
    }
}
