namespace Application.Features.Clients.Dtos;

/// <summary>Branding do portal apresentado ao cliente autenticado.</summary>
public sealed record ClientBrandingDto(
    string AppName,
    string? LogoUrl,
    string? PrimaryColor,
    string? BodyColor
);
