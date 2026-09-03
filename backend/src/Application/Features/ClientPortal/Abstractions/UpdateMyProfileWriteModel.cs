namespace Application.Features.ClientPortal.Abstractions;

/// <summary>Campos do perfil que o cliente pode alterar.</summary>
public sealed record UpdateMyProfileWriteModel(
    string? ContactEmail,
    string Phone,
    string? EmergencyContactName,
    string? EmergencyContactPhone);
