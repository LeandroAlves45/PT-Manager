using Application.Features.TrainerSettings.Dtos;
using TrainerSettingsEntity = Domain.Entities.TrainerSettings.TrainerSettings;

namespace Application.Features.TrainerSettings;

/// <summary>Converte TrainerSettings em contratos da Application sem AutoMapper.</summary>
public static class TrainerSettingsMappings
{
    public static TrainerSettingsDto ToDto(this TrainerSettingsEntity settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new TrainerSettingsDto(
            settings.Id,
            settings.TrainerId,
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
