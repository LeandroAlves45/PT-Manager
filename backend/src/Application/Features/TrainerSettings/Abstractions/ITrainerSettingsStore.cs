using Application.Features.TrainerSettings.Dtos;

namespace Application.Features.TrainerSettings.Abstractions;

/// <summary>Persiste mutações de definições do personal trainer de forma transacional.</summary>
public interface ITrainerSettingsStore
{
    Task<TrainerSettingsStoreResult> UpdateBrandingAsync(
        Guid trainerId,
        string appName,
        string? primaryColor,
        string? bodyColor,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<TrainerSettingsStoreResult> ResetBrandingColorsAsync(
        Guid trainerId,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<TrainerSettingsStoreResult> UpdateContactsAsync(
        Guid trainerId,
        string? phone,
        string? address,
        string? city,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<TrainerSettingsStoreResult> ChangeTimezoneAsync(
        Guid trainerId,
        string timezone,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<TrainerSettingsStoreResult> ReplaceLogoAsync(
        Guid trainerId,
        string logoUrl,
        string logoPublicId,
        Guid correlationId,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<TrainerSettingsStoreResult> RemoveLogoAsync(
        Guid trainerId,
        Guid correlationId,
        DateTime now,
        CancellationToken cancellationToken
    );
}
