namespace Application.Features.TrainerSettings.UpdateContacts;

/// <summary>Dados de contacto opcionais do personal trainer.</summary>
public sealed record UpdateContactsCommand(
    string? Phone,
    string? Address,
    string? City
);
