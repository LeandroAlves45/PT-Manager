namespace Application.Features.Administration.Overview;

/// <summary>
/// Contagens transversais a tenants para o superuser. A implementação ignora os Global
/// Query Filters de forma explícita e devolve apenas agregados.
/// </summary>
public interface IAdminOverviewQueries
{
    /// <summary>Lê as contagens dos catálogos globais e das filas privadas.</summary>
    Task<AdminOverviewDto> GetAsync(CancellationToken cancellationToken);
}
