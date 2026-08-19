using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Pagination;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Supplements.ListSupplementAssignments;

/// <summary>Lista atribuições tenant-safe com paginação.</summary>
public sealed class ListSupplementAssignmentsHandler
{
    private readonly IValidator<ListSupplementAssignmentsQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClientSupplementAssignmentQueries _queries;

    public ListSupplementAssignmentsHandler(
        IValidator<ListSupplementAssignmentsQuery> validator,
        ITenantContext tenantContext,
        IClientSupplementAssignmentQueries queries)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<PageResult<ClientSupplementAssignmentDto>>> HandleAsync(
        ListSupplementAssignmentsQuery query,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<ClientSupplementAssignmentDto>>.Failure(
                validation.ToApplicationError());

        var actor = SupplementActorAuthorization.RequireTrainer(_tenantContext);
        if (!actor.IsSuccess)
            return Result<PageResult<ClientSupplementAssignmentDto>>.Failure(actor.Error!);

        var page = await _queries.ListAsync(
            actor.Value.TrainerId,
            query.ClientId,
            query.Activity,
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken);

        return Result<PageResult<ClientSupplementAssignmentDto>>.Success(page);
    }
}
