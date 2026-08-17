using Application.Common.Abstractions;
using Application.Features.Sessions.Abstractions;
using Application.Features.Sessions.Dtos;
using Application.Results;

namespace Application.Features.Sessions.GetSession;

/// <summary>Obtém uma sessão visível ao personal trainer autenticado.</summary>
public sealed class GetSessionHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly ISessionQueries _queries;

    public GetSessionHandler(
        ITenantContext tenantContext,
        ISessionQueries queries)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(queries);

        _tenantContext = tenantContext;
        _queries = queries;
    }

    public async Task<Result<SessionDto>> HandleAsync(
        GetSessionQuery query,
        CancellationToken cancellationToken
    )
    {
        var tenant = SessionActorAuthorization.RequireTrainer(_tenantContext);
        if (!tenant.IsSuccess)
            return Result<SessionDto>.Failure(tenant.Error!);

        var session = await _queries.GetAsync(
            tenant.Value,
            query.SessionId,
            cancellationToken
        );

        return session is null
            ? Result<SessionDto>.Failure(SessionErrors.SessionNotFound)
            : Result<SessionDto>.Success(session);
    }
}
