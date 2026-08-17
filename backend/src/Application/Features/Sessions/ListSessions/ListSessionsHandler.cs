using Application.Common.Abstractions;
using Application.Features.Sessions.Abstractions;
using Application.Features.Sessions.Dtos;
using Application.Pagination;
using Application.Results;
using Application.Validation;
using FluentValidation;

namespace Application.Features.Sessions.ListSessions;

/// <summary>Lista sessões do personal trainer autenticado.</summary>
public sealed class ListSessionsHandler
{
    private readonly IValidator<ListSessionsQuery> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly ISessionQueries _queries;

    public ListSessionsHandler(
        IValidator<ListSessionsQuery> validator,
        ITenantContext tenantContext,
        ISessionQueries queries)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(queries);

        _validator = validator;
        _tenantContext = tenantContext;
        _queries = queries;
    }

    public async Task<Result<PageResult<SessionDto>>> HandleAsync(
        ListSessionsQuery query,
        CancellationToken cancellationToken
    )
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
            return Result<PageResult<SessionDto>>.Failure(validation.ToApplicationError());

        var tenant = SessionActorAuthorization.RequireTrainer(_tenantContext);
        if (!tenant.IsSuccess)
            return Result<PageResult<SessionDto>>.Failure(tenant.Error!);

        var page = await _queries.ListAsync(
            tenant.Value,
            query.ClientId,
            query.Status,
            query.StartsFrom?.ToUniversalTime(),
            query.StartsBefore?.ToUniversalTime(),
            new PageRequest(query.PageNumber, query.PageSize),
            cancellationToken
        );

        return Result<PageResult<SessionDto>>.Success(page);
    }
}
