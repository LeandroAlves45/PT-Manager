using Application.Common.Abstractions;
using Application.Features.Packs.ClientSessionPacks.Abstractions;
using Application.Features.Packs.ClientSessionPacks.Dtos;
using Application.Pagination;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Packs.ClientSessionPacks.ListClientSessionPacks;

/// <summary>Lista packs atribuídos visíveis no tenant.</summary>
public sealed class ListClientSessionPacksHandler
{
    private readonly IValidator<ListClientSessionPacksQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly IClientSessionPackQueries _queries;

    public ListClientSessionPacksHandler(
        IValidator<ListClientSessionPacksQuery> validator,
        ITenantContext tenantContext,
        IClientSessionPackQueries queries
    )
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(queries);

        _validator = validator;
        _tenantContext = tenantContext;
        _queries = queries;
    }

    public async Task<Result<PageResult<ClientSessionPackDto>>> HandleAsync(
        ListClientSessionPacksQuery query,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<ClientSessionPackDto>>.Failure(
                validation.ToApplicationError()
            );

        var tenant = _tenantContext.GetRequiredTrainerId();
        if (!tenant.IsSuccess)
            return Result<PageResult<ClientSessionPackDto>>.Failure(tenant.Error!);

        var page = await _queries.ListAsync(
            tenant.Value,
            query.ClientId,
            query.Activity,
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken
        );

        return Result<PageResult<ClientSessionPackDto>>.Success(page);
    }
}
