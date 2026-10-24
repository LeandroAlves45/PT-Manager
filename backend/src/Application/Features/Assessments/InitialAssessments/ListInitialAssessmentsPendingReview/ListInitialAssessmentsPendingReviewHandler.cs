using Application.Common.Abstractions;
using Application.Features.Assessments.InitialAssessments.Abstractions;
using Application.Features.Assessments.InitialAssessments.Dtos;
using Application.Pagination;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Assessments.InitialAssessments.ListInitialAssessmentsPendingReview;

/// <summary>Página das avaliações iniciais por rever.</summary>
public sealed record ListInitialAssessmentsPendingReviewQuery(int PageNumber = 1, int PageSize = 50);

/// <summary>Valida a paginação da lista.</summary>
public sealed class ListInitialAssessmentsPendingReviewQueryValidator
    : AbstractValidator<ListInitialAssessmentsPendingReviewQuery>
{
    public ListInitialAssessmentsPendingReviewQueryValidator() =>
        this.ApplyPaginationRules(query => query.PageNumber, query => query.PageSize);
}

/// <summary>
/// Lista as avaliações iniciais que os clientes ativos gravaram e o personal trainer ainda não
/// marcou como revistas, da mais antiga para a mais recente. As criadas pelo personal trainer
/// nunca entram.
/// </summary>
public sealed class ListInitialAssessmentsPendingReviewHandler
{
    private readonly IValidator<ListInitialAssessmentsPendingReviewQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IInitialAssessmentQueries _queries;

    public ListInitialAssessmentsPendingReviewHandler(
        IValidator<ListInitialAssessmentsPendingReviewQuery> validator,
        ITenantContext tenantContext,
        IInitialAssessmentQueries queries)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<PageResult<InitialAssessmentPendingReviewDto>>> HandleAsync(
        ListInitialAssessmentsPendingReviewQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<InitialAssessmentPendingReviewDto>>.Failure(
                validation.ToApplicationError());

        var tenant = AssessmentActorAuthorization.RequireTrainer(_tenantContext);
        if (!tenant.IsSuccess)
            return Result<PageResult<InitialAssessmentPendingReviewDto>>.Failure(tenant.Error!);

        var page = await _queries.ListPendingReviewAsync(
            tenant.Value,
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken);

        return Result<PageResult<InitialAssessmentPendingReviewDto>>.Success(page);
    }
}
