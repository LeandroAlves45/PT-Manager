using Application.Common.Abstractions;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Pagination;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Supplements.ListGlobalSupplements;

/// <summary>Lista suplementos globais para um superuser autorizado.</summary>
public sealed class ListGlobalSupplementsHandler
{
    private readonly IValidator<ListGlobalSupplementsQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IGlobalSupplementQueries _queries;

    public ListGlobalSupplementsHandler(
        IValidator<ListGlobalSupplementsQuery> validator,
        ITenantContext tenantContext,
        IGlobalSupplementQueries queries)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<PageResult<GlobalSupplementDto>>> HandleAsync(
        ListGlobalSupplementsQuery query,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<GlobalSupplementDto>>.Failure(
                validation.ToApplicationError());

        var actor = SupplementActorAuthorization.RequireAdministrator(_tenantContext);
        if (!actor.IsSuccess)
            return Result<PageResult<GlobalSupplementDto>>.Failure(actor.Error!);

        var page = await _queries.ListAsync(
            string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            query.Activity,
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken);

        return Result<PageResult<GlobalSupplementDto>>.Success(page);
    }
}
