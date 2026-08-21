using TrainerSettingsEntity = Domain.Entities.TrainerSettings.TrainerSettings;

namespace Application.Features.TrainerSettings.Abstractions;

/// <summary>Representa os outcomes possíveis de uma mutação de TrainerSettings.</summary>
public sealed class TrainerSettingsStoreResult
{

    public enum Status
    {
        Updated,
        ScheduleConflict
    }

    public Status Kind { get; }
    public TrainerSettingsEntity? Settings { get; }
    public string? PreviousLogoPublicId { get; }

    private TrainerSettingsStoreResult(
        Status kind,
        TrainerSettingsEntity? settings,
        string? previousLogoPublicId
    )
    {
        Kind = kind;
        Settings = settings;
        PreviousLogoPublicId = previousLogoPublicId;
    }

    public static TrainerSettingsStoreResult Updated(
        TrainerSettingsEntity settings,
        string? previousLogoPublicId = null
    )
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new TrainerSettingsStoreResult(Status.Updated, settings, previousLogoPublicId);
    }

    public static TrainerSettingsStoreResult Conflict() =>
        new(Status.ScheduleConflict, null, null);
}
