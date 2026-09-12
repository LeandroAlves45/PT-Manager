using Microsoft.Extensions.Logging;

namespace Infrastructure.Media;

/// <summary>IDs estáveis dos eventos operacionais de media.</summary>
public static class MediaLogEvents
{
    public static readonly EventId StorageFailure = new(4000, nameof(StorageFailure));
    public static readonly EventId StorageInvalidResponse = new(4001, nameof(StorageInvalidResponse));
    public static readonly EventId StorageDeletionRefused = new(4002, nameof(StorageDeletionRefused));
    public static readonly EventId ModerationUnavailable = new(4003, nameof(ModerationUnavailable));
    public static readonly EventId ModerationQuotaExhausted = new(4004, nameof(ModerationQuotaExhausted));
    public static readonly EventId ModerationNotApproved = new(4005, nameof(ModerationNotApproved));
}
