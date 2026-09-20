namespace Application.Features.Clients.GetClientSummary;

/// <summary>Pede o resumo do progresso de um cliente do tenant efetivo.</summary>
public sealed record GetClientSummaryQuery(Guid ClientId);
