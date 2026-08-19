using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Pagination;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Supplements.ListMySupplementAssignments;

/// <summary>Lista as prescrições ativas do cliente autenticado.</summary>
public sealed class ListMySupplementAssignmentsHandler
{
    private readonly IValidator<ListMySupplementAssignmentsQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClientSupplementAssignmentQueries _queries;

    public ListMySupplementAssignmentsHandler(
        IValidator<ListMySupplementAssignmentsQuery> validator,
        ITenantContext tenantContext,
        IClientSupplementAssignmentQueries queries)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<PageResult<MySupplementAssignmentDto>>> HandleAsync(
        ListMySupplementAssignmentsQuery query,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<MySupplementAssignmentDto>>.Failure(
                validation.ToApplicationError());

        var actor = SupplementActorAuthorization.RequireClient(_tenantContext);
        if (!actor.IsSuccess)
            return Result<PageResult<MySupplementAssignmentDto>>.Failure(actor.Error!);

        var page = await _queries.ListMyActiveAsync(
            actor.Value.TrainerId, actor.Value.UserId,
            new PageRequest(query.PageNumber, query.PageSize), cancellationToken);
        return Result<PageResult<MySupplementAssignmentDto>>.Success(page);
    }
}
