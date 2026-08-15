namespace Application.Features.Packs.ClientSessionPacks.CancelClientSessionPack;

/// <summary>Solicita o cancelamento seguro de um pack atribuído.</summary>
public sealed record CancelClientSessionPackCommand(Guid ClientSessionPackId);
