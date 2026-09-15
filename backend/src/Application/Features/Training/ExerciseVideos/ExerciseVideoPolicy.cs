using Application.Features.Training.ExerciseVideos.Abstractions;
using Domain.Entities.Training;

namespace Application.Features.Training.ExerciseVideos;

/// <summary>
/// Política técnica aprovada para vídeos de exercício. Os limites são decisões de
/// produto fechadas antes da implementação e congeladas por teste.
/// </summary>
public static class ExerciseVideoPolicy
{
    public const long MaxSizeBytes = 100L * 1024 * 1024; // 100MB
    public const int MaxLongSidePixels = 1920;
    public const int MinShortSidePixels = 240;
    public static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(3);
    public static IReadOnlySet<string> AcceptedContentTypes = ExerciseVideo.AcceptedContentTypes;
    public static readonly IReadOnlySet<string> AcceptedVideoCodecs =
        new HashSet<string>(StringComparer.Ordinal) { "mp4a" };

    /// <summary>
    /// Avalia a metadata real contra a política. Devolve o código estável de recusa
    /// ou null quando o vídeo é aceite.
    /// </summary>
    public static string? Evaluate(
        string declaredContentType,
        VideoTechnicalMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var expectedContainer = declaredContentType switch
        {
            "video/mp4" => VideoContainer.Mp4,
            "video/quicktime" => VideoContainer.QuickTime,
            _ => (VideoContainer?)null
        };

        if (expectedContainer is null || expectedContainer != metadata.Container)
            return "exercise_video_container_mismatch";

        if (metadata.VideoTrackCount != 1 || metadata.AudioTrackCount > 1)
            return "exercise_video_track_layout_unsupported";

        if (!AcceptedVideoCodecs.Contains(metadata.VideoCodec))
            return "exercise_video_codec_unsupported";

        if (metadata.AudioTrackCount == 1 &&
            (metadata.AudioCodec is null || !AcceptedVideoCodecs.Contains(metadata.AudioCodec)))
            return "exercise_video_audio_codec_unsupported";

        if (metadata.DurationMilliseconds <= 0)
            return "exercise_video_duration_invalid";

        var longSide = Math.Max(metadata.Width, metadata.Height);
        var shortSide = Math.Min(metadata.Width, metadata.Height);

        if (longSide > MaxLongSidePixels)
            return "exercise_video_resolution_exceeded";

        if (shortSide < MinShortSidePixels)
            return "exercise_video_resolution_too_small";

        return null;
    }

    /// <summary>
    /// Compara o tipo declarado com o tipo de armazenamento. Parâmetros (por exemplo
    /// <c>charset</c>) são ignorados; um tipo ausente no fornecedor é recusa.
    /// </summary>
    public static bool ContentTypesMatch(string declaredContentType, string? storedContentType)
    {
        var declared = NormalizeContentType(declaredContentType);
        var stored = NormalizeContentType(storedContentType);
        return declared is not null &&
            string.Equals(declared, stored, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeContentType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var separator = value.IndexOf(';', StringComparison.Ordinal);
        var mediaType = (separator < 0 ? value : value[..separator]).Trim();
        return mediaType.Length == 0 ? null : mediaType;
    }
}
