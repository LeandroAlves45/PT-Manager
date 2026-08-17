namespace Application.Features.Sessions.ChangeSessionPack;

/// <summary>Associa, troca ou remove o pack de uma sessão Scheduled.</summary>
public sealed record ChangeSessionPackCommand(
    Guid SessionId,
    Guid? ClientSessionPackId
);
