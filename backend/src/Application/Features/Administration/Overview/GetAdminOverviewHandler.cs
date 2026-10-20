using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Common.Time;
using Application.Results;

namespace Application.Features.Administration.Overview;

/// <summary>Pede as contagens da visão geral do superuser.</summary>
public sealed record GetAdminOverviewQuery;

/// <summary>Autoriza o superuser e devolve as contagens dos catálogos e das filas.</summary>
public sealed class GetAdminOverviewHandler
{
    private readonly ITenantContext _tenantContext;
    private static readonly TimeZoneInfo PlatformTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    private readonly IAdminOverviewQueries _queries;
    private readonly IClock _clock;

    public GetAdminOverviewHandler(
        ITenantContext tenantContext,
        IAdminOverviewQueries queries,
        IClock clock)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
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

        var today = LocalDates.Today(_clock.UtcNow, PlatformTimeZone);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var revenuePeriod = LocalDates.ToUtcRange(
            monthStart,
            monthStart.AddMonths(1),
            PlatformTimeZone);

        return Result<AdminOverviewDto>.Success(
            await _queries.GetAsync(revenuePeriod, cancellationToken));
    }
}
