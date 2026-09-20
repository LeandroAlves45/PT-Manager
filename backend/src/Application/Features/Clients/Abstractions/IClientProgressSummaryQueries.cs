using Application.Features.Clients.Dtos;

namespace Application.Features.Clients.Abstractions;

/// <summary>
/// Porta de leitura do resumo do progresso. Os limites de janela chegam já resolvidos no
/// fuso do personal trainer; a implementação não conhece relógio nem fuso.
/// </summary>
public interface IClientProgressSummaryQueries
{
    Task<ClientProgressSnapshot?> GetAsync(
        Guid clientId,
        DateOnly weightWindowStart,
        DateTimeOffset logsFromUtc,
        DateTimeOffset logsToUtc,
        CancellationToken cancellationToken);
}
