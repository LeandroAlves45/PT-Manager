namespace Application.Features.Clients.ArchiveClients;

/// <summary>Solicita o arquivo reversível de um cliente.</summary>
/// <param name="ClientId">Identificador da ficha.</param>
public sealed record ArchiveClientCommand(Guid ClientId);
