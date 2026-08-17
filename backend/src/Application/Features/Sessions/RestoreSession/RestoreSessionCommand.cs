namespace Application.Features.Sessions.RestoreSession;

/// <summary>Repõe uma sessão terminal em Scheduled.</summary>
public sealed record RestoreSessionCommand(Guid SessionId);
