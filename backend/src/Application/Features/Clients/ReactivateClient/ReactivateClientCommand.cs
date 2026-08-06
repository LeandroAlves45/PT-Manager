namespace Application.Features.Clients.ReactivateClient;

/// <summary>Solicita a reativação de um cliente previamente arquivado.</summary>
/// <param name="ClientId">Identificador da ficha.</param>
public sealed record ReactivateClientCommand(Guid ClientId);
