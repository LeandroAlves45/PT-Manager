namespace Application.Features.Sessions.CancelSessionByClient;

/// <summary>Regista que o cancelamento foi solicitado pelo cliente.</summary>
public sealed record CancelSessionByClientCommand(Guid SessionId);
