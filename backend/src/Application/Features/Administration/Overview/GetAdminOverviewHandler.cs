using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Results;

namespace Application.Features.Administration.Overview;

/// <summary>Pede as contagens da visão geral do superuser.</summary>
public sealed record GetAdminOverviewQuery;

/// <summary>Autoriza o superuser e devolve as contagens dos catálogos e das filas.</summary>
public sealed class GetAdminOverviewHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IAdminOverviewQueries _queries;

    public GetAdminOverviewHandler(
        ITenantContext tenantContext,
        IAdminOverviewQueries queries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<AdminOverviewDto>> HandleAsync(
        GetAdminOverviewQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var actor = ActorAuthorization.RequireAdministrator(
            _tenantContext,
            AdminOverviewErrors.AdministratorOnly);
        if (!actor.IsSuccess)
            return Result<AdminOverviewDto>.Failure(actor.Error!);

        return Result<AdminOverviewDto>.Success(await _queries.GetAsync(cancellationToken));
    }
}
