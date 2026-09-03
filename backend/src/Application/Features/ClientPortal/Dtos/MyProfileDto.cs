namespace Application.Features.ClientPortal.Dtos;

/// <summary>Perfil do cliente autenticado, tal como ele próprio o vê.</summary>
public sealed record MyProfileDto(
    string Name,
    string? ContactEmail,
    string Phone,
    DateOnly BirthDate,
    string Sex,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? AvatarUrl,
    DateTime UpdatedAt);
