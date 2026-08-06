namespace Application.Features.Clients.GetClient;

/// <summary>Solicita o detalhe de um cliente visível no tenant efetivo.</summary>
/// <param name="ClientId">Identificador da ficha.</param>
public sealed record GetClientQuery(Guid ClientId);
