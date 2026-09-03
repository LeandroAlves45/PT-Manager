namespace Application.Features.ClientPortal.UpdateMyProfile;

/// <summary>Campos do perfil que o cliente pode alterar.</summary>
public sealed record UpdateMyProfileCommand(
    string? ContactEmail,
    string Phone,
    string? EmergencyContactName,
    string? EmergencyContactPhone
);
