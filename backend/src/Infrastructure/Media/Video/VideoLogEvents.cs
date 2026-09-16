using Microsoft.Extensions.Logging;

namespace Infrastructure.Media.Video;

/// <summary>IDs estáveis dos eventos operacionais de vídeo gerido.</summary>
public static class VideoLogEvents
{
    public static readonly EventId StorageFailure = new(4100, nameof(StorageFailure));
    public static readonly EventId StorageObjectKeyRefused = new(4101, nameof(StorageObjectKeyRefused));
    public static readonly EventId ProbeUnsupported = new(4102, nameof(ProbeUnsupported));
    public static readonly EventId ProbeObjectChanged = new(4103, nameof(ProbeObjectChanged));
    public static readonly EventId AdministrativePlaybackIssued = new(4104, nameof(AdministrativePlaybackIssued));
}
