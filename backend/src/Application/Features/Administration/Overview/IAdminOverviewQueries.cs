using Application.Common.Time;

namespace Application.Features.Administration.Overview;

/// <summary>
/// Contagens transversais a tenants para o superuser. A implementação ignora os Global
/// Query Filters de forma explícita e devolve apenas agregados.
/// </summary>
public interface IAdminOverviewQueries
{
    /// <summary>Contagens de catálogos, moderação e treinadores, e o líquido recebido no período.</summary>
    /// <param name="revenuePeriod">Mês de calendário em Europe/Lisbon, em UTC meio-aberto.</param>
    /// <param name="cancellationToken">Cancela a leitura.</param>
    Task<AdminOverviewDto> GetAsync(UtcRange revenuePeriod, CancellationToken cancellationToken);
}
