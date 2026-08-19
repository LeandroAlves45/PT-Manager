using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Pagination;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Supplements.ListSupplements;

/// <summary>Lista suplementos visíveis ao personal trainer sem carregar entidades.</summary>
public sealed class ListSupplementsHandler
{
    private readonly IValidator<ListSupplementsQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly ISupplementQueries _queries;

    public ListSupplementsHandler(
        IValidator<ListSupplementsQuery> validator,
        ITenantContext tenantContext,
        ISupplementQueries queries)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<PageResult<SupplementDto>>> HandleAsync(
        ListSupplementsQuery query,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<SupplementDto>>.Failure(validation.ToApplicationError());

        var actor = SupplementActorAuthorization.RequireTrainer(_tenantContext);
        if (!actor.IsSuccess)
            return Result<PageResult<SupplementDto>>.Failure(actor.Error!);

        var page = await _queries.ListAsync(
            actor.Value.TrainerId,
            string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            query.Activity,
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken);

        return Result<PageResult<SupplementDto>>.Success(page);
    }
}
