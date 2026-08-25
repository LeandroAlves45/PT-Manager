namespace Application.Features.Authentication.InviteClient;

/// <summary>Solicita um convite para uma ficha do tenant autenticado.</summary>
public sealed record InviteClientCommand(Guid ClientId, string Email);
