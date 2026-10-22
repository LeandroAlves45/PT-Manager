namespace Application.Features.ClientPortal.Abstractions;

/// <summary>Campos do perfil que o cliente pode alterar.</summary>
public sealed record UpdateMyProfileWriteModel(
    string Name,
    string Phone,
    string? EmergencyContactName,
    string? EmergencyContactPhone);
