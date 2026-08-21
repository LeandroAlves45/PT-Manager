using Application.Errors;

namespace Application.Features.TrainerSettings;

/// <summary>Disponibiliza erros estáveis dos casos de uso de TrainerSettings.</summary>
public static class TrainerSettingsErrors
{
    public static readonly Error TrainerOnly = Error.Create(
        "trainer_settings_trainer_only",
        ErrorCategory.Forbidden,
        "Only a personal trainer can manage their own settings."
    );

    public static readonly Error ClientOnly = Error.Create(
        "trainer_settings_client_only",
        ErrorCategory.Forbidden,
        "Only the associated client can read branding."
    );

    public static readonly Error InvalidTimezone = Error.Validation([
        new ValidationError(
            "Timezone",
            "trainer_settings_invalid_timezone",
            "Timezone is not a known IANA identifier."
        )
    ]);

    public static readonly Error ScheduleConflict = Error.Create(
        "trainer_settings_schedule_conflict",
        ErrorCategory.Conflict,
        "Changing the timezone would create two scheduled sessions for the same client on" +
        " the same local day."
    );

    public static readonly Error UnsupportedMediaType = Error.Validation([
        new ValidationError(
            "Logo",
            "trainer_settings_unsupported_media_type",
            "Logo must be PNG, JPEG, or WEBP."
        )
    ]);

    public static readonly Error MediaTooLarge = Error.Validation([
        new ValidationError(
            "Logo",
            "trainer_settings_media_too_large",
            "Logo cannot exceed 5MB."
        )
    ]);

    public static readonly Error MediaUploadFailed = Error.Create(
        "trainer_settings_media_upload_failed",
        ErrorCategory.Internal,
        "The logo could not be uploaded. No changes were made.");

    public static readonly Error PersistenceFailed = Error.Create(
        "trainer_settings_persistence_failed",
        ErrorCategory.Internal,
        "The logo was uploaded, but settings could not be saved. The uploaded asset was removed.");

    public static readonly Error LogoCompensationFailed = Error.Create(
        "logo_compensation_failed",
        ErrorCategory.Internal,
        "The logo was uploaded but settings could not be saved, and the upload asset could" +
        " not be removed. Manual cleanup is required."
    );

    public static readonly Error BrandingNotAvailable = Error.Create(
        "branding_not_available",
        ErrorCategory.NotFound,
        "Branding is not available for this client."
    );
}
