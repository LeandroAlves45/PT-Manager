namespace Application.Features.Training.ExerciseVideos.Abstractions;

/// <summary>Containers reconhecidos pelo probe.</summary>
public enum VideoContainer
{
    Mp4,
    QuickTime
}

/// <summary>Metadados técnicos extraídos do próprio container.</summary>
public sealed record VideoTechnicalMetadata(
    VideoContainer Container,
    long DurationMilliseconds,
    int Width,
    int Height,
    string VideoCodec,
    string? AudioCodec,
    int VideoTrackCount,
    int AudioTrackCount);

/// <summary>Estados esperados de uma operação de probe.</summary>
public enum VideoProbeStatus
{
    Probed,
    Unsupported,
    ObjectChanged,
    NotFound,
    Disabled,
    TransientFailure
}

public sealed record VideoProbeOutcome(
    VideoProbeStatus Status,
    VideoTechnicalMetadata? Metadata = null,
    string? FailureCode = null)
{
    public static VideoProbeOutcome Probed(VideoTechnicalMetadata metadata) =>
        new(VideoProbeStatus.Probed, metadata ?? throw new ArgumentNullException(nameof(metadata)));

    public static VideoProbeOutcome Failure(VideoProbeStatus status, string failureCode) =>
        status == VideoProbeStatus.Probed
            ? throw new ArgumentException("A failure cannot be Probed.", nameof(status))
            : new(status, null, failureCode);
}
