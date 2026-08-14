using Application.Common.Abstractions;
using Application.Features.Packs.PackTypes.Abstractions;
using Application.Features.Packs.PackTypes.Dtos;
using Application.Pagination;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Packs.PackTypes.ListPackTypes;

/// <summary>Lista tipos de pack do tenant com ordenação determinística.</summary>
public sealed class ListPackTypesHandler
{
    private readonly IValidator<ListPackTypesQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IPackTypeQueries _queries;

    public ListPackTypesHandler(
        IValidator<ListPackTypesQuery> validator,
        ITenantContext tenantContext,
        IPackTypeQueries queries
    )
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(queries);
        _validator = validator;
        _tenantContext = tenantContext;
        _queries = queries;
    }

    public async Task<Result<PageResult<PackTypeDto>>> HandleAsync(
        ListPackTypesQuery query,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<PackTypeDto>>.Failure(validation.ToApplicationError());

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<PageResult<PackTypeDto>>.Failure(tenant.Error!);

        var page = await _queries.ListAsync(
            tenant.Value,
            string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            query.Activity,
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken
        );

        return Result<PageResult<PackTypeDto>>.Success(page);
    }
}
