namespace Application.Features.Clients.ArchiveClient;

/// <summary>Solicita o arquivo reversível de um cliente.</summary>
/// <param name="ClientId">Identificador da ficha.</param>
public sealed record ArchiveClientCommand(Guid ClientId);
