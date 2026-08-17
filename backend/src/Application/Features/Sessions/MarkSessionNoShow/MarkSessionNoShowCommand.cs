namespace Application.Features.Sessions.MarkSessionNoShow;

/// <summary>Marca falta e debita o pack opcional.</summary>
public sealed record MarkSessionNoShowCommand(Guid SessionId);
