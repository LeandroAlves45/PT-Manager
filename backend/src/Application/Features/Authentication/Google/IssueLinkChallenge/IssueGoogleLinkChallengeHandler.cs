using Application.Common.Abstractions;
using Application.Features.Authentication.Google.Abstractions;
using Application.Features.Authentication.Google.Dtos;
using Application.Results;
using Domain.Entities.Identity;

namespace Application.Features.Authentication.Google.IssueLinkChallenge;

/// <summary>Emite um nonce que só pode associar Google ao utilizador atual.</summary>
public sealed class IssueGoogleLinkChallengeHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IExternalChallengeStore _store;
    private readonly IClock _clock;

    public IssueGoogleLinkChallengeHandler(
        ITenantContext tenantContext,
        IExternalChallengeStore store,
        IClock clock)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<GoogleChallengeDto>> HandleAsync(CancellationToken cancellationToken)
    {
        if (!_tenantContext.UserId.HasValue || _tenantContext.UserId == Guid.Empty ||
            _tenantContext.Role is not ("trainer" or "client" or "superuser"))
            return Result<GoogleChallengeDto>.Failure(
                AuthenticationErrors.AuthenticatedAccountRequired);

        var now = _clock.UtcNow;
        var issued = await _store.IssueAsync(
            ExternalAuthenticationChallenge.LinkPurpose,
            _tenantContext.UserId.Value,
            now.AddMinutes(5),
            now,
            cancellationToken);

        return Result<GoogleChallengeDto>.Success(
            new GoogleChallengeDto(issued.RawNonce, issued.ExpiresAt));
    }
}
