namespace Application.Features.ClientPortal.UpdateMyProfile;

/// <summary>Campos do perfil que o cliente pode alterar. O email da ficha fica só com o personal trainer.</summary>
public sealed record UpdateMyProfileCommand(
    string Name,
    string Phone,
    string? EmergencyContactName,
    string? EmergencyContactPhone
);
