using Application.Common;
using Application.Common.Abstractions;
using Application.Common.Authorization;
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
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<PageResult<TrainingPlanSummaryDto>>> HandleAsync(
        ListTrainingPlansQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<TrainingPlanSummaryDto>>.Failure(validation.ToApplicationError());

        var actor = ActorAuthorization.RequireTrainer(_tenantContext, TrainingErrors.TrainingPlanTrainerOnly);
        if (!actor.IsSuccess)
            return Result<PageResult<TrainingPlanSummaryDto>>.Failure(actor.Error!);

        var result = await _queries.ListAsync(
            query.ClientId,
            SearchTerm.Normalize(query.Search),
            query.Activity,
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken);
        return Result<PageResult<TrainingPlanSummaryDto>>.Success(result);
    }
}
