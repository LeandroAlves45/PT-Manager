namespace Application.Features.Packs.ClientSessionPacks.ListUsableClientSessionPacks;

/// <summary>Solicita todos os packs utilizáveis de um cliente.</summary>
public sealed record ListUsableClientSessionPacksQuery(Guid ClientId);
