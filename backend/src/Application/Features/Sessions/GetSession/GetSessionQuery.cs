namespace Application.Features.Sessions.GetSession;

/// <summary>Obtém uma sessão do tenant efetivo.</summary>
public sealed record GetSessionQuery(Guid SessionId);
