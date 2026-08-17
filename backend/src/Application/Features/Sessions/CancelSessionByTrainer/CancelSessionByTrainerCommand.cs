namespace Application.Features.Sessions.CancelSessionByTrainer;

/// <summary>Cancela uma sessão por decisão do personal trainer.</summary>
public sealed record CancelSessionByTrainerCommand(Guid SessionId);
