using Application.Common.Abstractions;
using Application.Features.Assessments.InitialAssessments.Abstractions;
using Application.Features.Assessments.InitialAssessments.Dtos;
using Application.Results;

namespace Application.Features.Assessments.InitialAssessments.GetInitialAssessment;

/// <summary>Obtém uma avaliação inicial visível ao personal trainer.</summary>
public sealed class GetInitialAssessmentHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IInitialAssessmentQueries _queries;

    public GetInitialAssessmentHandler(
        ITenantContext tenantContext,
        IInitialAssessmentQueries queries
    )
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(queries);
        _tenantContext = tenantContext;
        _queries = queries;
    }

    public async Task<Result<InitialAssessmentDto?>> HandleAsync(
        GetInitialAssessmentQuery query,
        CancellationToken cancellationToken
    )
    {
        var tenant = AssessmentActorAuthorization.RequireTrainer(_tenantContext);
        if (!tenant.IsSuccess)
            return Result<InitialAssessmentDto?>.Failure(tenant.Error!);

        var assessment = await _queries.GetByClientAsync(
            tenant.Value,
            query.ClientId,
            cancellationToken
        );

        return assessment is null
            ? Result<InitialAssessmentDto?>.Failure(AssessmentErrors.InitialAssessmentNotFound)
            : Result<InitialAssessmentDto?>.Success(assessment);
    }
}
