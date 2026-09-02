using Application.Features.TrainerSettings.Dtos;

namespace Api.Contracts.TrainerSettings;

/// <summary>Branding editável. Cores nulas repõem o tema padrão.</summary>
public sealed record UpdateBrandingRequest(
    string AppName,
    string? PrimaryColor,
    string? BodyColor);

/// <summary>Contactos opcionais do personal trainer.</summary>
public sealed record UpdateContactsRequest(
    string? Phone,
    string? Address,
    string? City);

/// <summary>Novo timezone IANA.</summary>
public sealed record ChangeTimezoneRequest(string Timezone);

/// <summary>Definições completas visíveis apenas para o próprio personal trainer.</summary>
public sealed record TrainerSettingsResponse(
    string AppName,
    string? LogoUrl,
    string? PrimaryColor,
    string? BodyColor,
    string? Phone,
    string? Address,
    string? City,
    string Timezone,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>Projeta as definições da Application.</summary>
    public static TrainerSettingsResponse From(TrainerSettingsDto settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new(
            settings.AppName,
            settings.LogoUrl,
            settings.PrimaryColor,
            settings.BodyColor,
            settings.Phone,
            settings.Address,
            settings.City,
            settings.Timezone,
            settings.CreatedAt,
            settings.UpdatedAt
        );
    }
}
