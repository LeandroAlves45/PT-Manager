namespace Application.Features.Sessions.CompleteSession;

/// <summary>Conclui uma sessão e debita o pack opcional.</summary>
public sealed record CompleteSessionCommand(Guid SessionId);
