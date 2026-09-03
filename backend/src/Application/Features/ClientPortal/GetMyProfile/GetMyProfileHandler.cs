using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.ClientPortal.Abstractions;
using Application.Features.ClientPortal.Dtos;
using Application.Results;

namespace Application.Features.ClientPortal.GetMyProfile;

/// <summary>Devolve o perfil do cliente autenticado.</summary>
public sealed class GetMyProfileHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IMyProfileQueries _queries;

    public GetMyProfileHandler(
        ITenantContext tenantContext,
        IMyProfileQueries queries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    public async Task<Result<MyProfileDto>> HandleAsync(
        CancellationToken cancellationToken)
    {
        var actor = ActorAuthorization.RequireClient(
            _tenantContext,
            ClientPortalErrors.ClientOnly);
        if (!actor.IsSuccess)
            return Result<MyProfileDto>.Failure(actor.Error!);

        var profile = await _queries.GetAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            cancellationToken);

        return profile is null
            ? Result<MyProfileDto>.Failure(ClientPortalErrors.ProfileNotAvailable)
            : Result<MyProfileDto>.Success(profile);
    }
}
