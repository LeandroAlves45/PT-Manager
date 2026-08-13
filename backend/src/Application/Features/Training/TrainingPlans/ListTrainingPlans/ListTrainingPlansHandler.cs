using Application.Common.Abstractions;
using Application.Features.Training.TrainingPlans.Abstractions;
using Application.Features.Training.TrainingPlans.Dtos;
using Application.Pagination;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.TrainingPlans.ListTrainingPlans;

/// <summary>Lista planos de treino visíveis com ordenação determinística.</summary>
public sealed class ListTrainingPlansHandler
{
    private readonly IValidator<ListTrainingPlansQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly ITrainingPlanQueries _queries;

    public ListTrainingPlansHandler(
        IValidator<ListTrainingPlansQuery> validator,
        ITenantContext tenantContext,
        ITrainingPlanQueries queries)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(queries);

        _validator = validator;
        _tenantContext = tenantContext;
        _queries = queries;
    }

    public async Task<Result<PageResult<TrainingPlanSummaryDto>>> HandleAsync(
        ListTrainingPlansQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<TrainingPlanSummaryDto>>.Failure(validation.ToApplicationError());

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<PageResult<TrainingPlanSummaryDto>>.Failure(tenant.Error!);

        var result = await _queries.ListAsync(
            query.ClientId,
            string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            query.Activity,
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        return Result<PageResult<TrainingPlanSummaryDto>>.Success(result);
    }
}
