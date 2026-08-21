namespace Application.Features.TrainerSettings.UpdateBranding;

/// <summary>Dados editáveis de branding. Cores null repõem o tema padrão.</summary>
public sealed record UpdateBrandingCommand(
    string AppName,
    string? PrimaryColor,
    string? BodyColor
);
