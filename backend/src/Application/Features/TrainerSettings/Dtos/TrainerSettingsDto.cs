namespace Application.Features.TrainerSettings.Dtos;

/// <summary>Representa as definições completas visíveis apenas ao próprio personal trainer.</summary>
public sealed record TrainerSettingsDto(
    Guid Id,
    Guid TrainerId,
    string AppName,
    string? LogoUrl,
    string? PrimaryColor,
    string? BodyColor,
    string? Phone,
    string? Address,
    string? City,
    string Timezone,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
