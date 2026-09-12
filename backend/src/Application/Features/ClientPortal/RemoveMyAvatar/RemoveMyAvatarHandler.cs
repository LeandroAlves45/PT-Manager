using Application.Common.Abstractions;
using Application.Common.Authorization;
using Application.Features.ClientPortal.Abstractions;
using Application.Features.ClientPortal.Dtos;
using Application.Results;

namespace Application.Features.ClientPortal.RemoveMyAvatar;

/// <summary>Elimina a fotografia de perfil do cliente autenticado.</summary>
public sealed class RemoveMyAvatarHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly IMyAvatarStore _store;

    public RemoveMyAvatarHandler(
        ITenantContext tenantContext,
        IClock clock,
        IMyAvatarStore store)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<Result<MyProfileDto>> HandleAsync(
        CancellationToken cancellationToken)
    {
        var actor = ActorAuthorization.RequireClient(
            _tenantContext,
            ClientPortalErrors.ClientOnly);
        if (!actor.IsSuccess)
            return Result<MyProfileDto>.Failure(actor.Error!);

        var outcome = await _store.RemoveAsync(
            actor.Value.TrainerId,
            actor.Value.UserId,
            Guid.NewGuid(),
            _clock.UtcNow,
            cancellationToken);

        return outcome.Status == MyAvatarStatus.Updated
            ? Result<MyProfileDto>.Success(outcome.Profile!)
            : Result<MyProfileDto>.Failure(ClientPortalErrors.ProfileNotAvailable);
    }
}
