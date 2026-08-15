namespace Application.Features.Packs.ClientSessionPacks.GetClientSessionPack;

/// <summary>Solicita um pack atribuído pelo identificador.</summary>
public sealed record GetClientSessionPackQuery(Guid ClientSessionPackId);
