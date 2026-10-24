using Application.Features.Assessments.InitialAssessments.Dtos;
using Application.Pagination;

namespace Application.Features.Assessments.InitialAssessments.Abstractions;

/// <summary>Consulta avaliações iniciais no tenant efetivo.</summary>
public interface IInitialAssessmentQueries
{
    Task<InitialAssessmentDto?> GetByClientAsync(
        Guid trainerId,
        Guid clientId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Indica se o cliente ativo desta conta ainda não tem avaliação inicial. Null quando a
    /// conta não tem ficha ativa neste tenant.
    /// </summary>
    Task<bool?> IsPendingForClientUserAsync(
        Guid trainerId,
        Guid clientUserId,
        CancellationToken cancellationToken
    );

    /// <summary>Avaliações iniciais gravadas por clientes ativos e ainda por rever.</summary>
    Task<PageResult<InitialAssessmentPendingReviewDto>> ListPendingReviewAsync(
        Guid trainerId,
        PageRequest page,
        CancellationToken cancellationToken
    );
}
