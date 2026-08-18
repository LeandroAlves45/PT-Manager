using Application.Features.Assessments.InitialAssessments.Dtos;

namespace Application.Features.Assessments.InitialAssessments.Abstractions;

/// <summary>Consulta avaliações iniciais no tenant efetivo.</summary>
public interface IInitialAssessmentQueries
{
    Task<InitialAssessmentDto?> GetByClientAsync(
        Guid trainerId,
        Guid clientId,
        CancellationToken cancellationToken
    );
}
