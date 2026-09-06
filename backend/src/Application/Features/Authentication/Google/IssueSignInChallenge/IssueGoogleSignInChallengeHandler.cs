using Application.Common.Abstractions;
using Application.Features.Authentication.Google.Abstractions;
using Application.Features.Authentication.Google.Dtos;
using Domain.Entities.Identity;

namespace Application.Features.Authentication.Google.IssueSignInChallenge;

/// <summary>Emite um nonce curto que só pode autorizar Google Sign-In.</summary>
public sealed class IssueGoogleSignInChallengeHandler
{
    private readonly IExternalChallengeStore _store;
    private readonly IClock _clock;

    public IssueGoogleSignInChallengeHandler(IExternalChallengeStore store, IClock clock)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<GoogleChallengeDto> HandleAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var issued = await _store.IssueAsync(
            ExternalAuthenticationChallenge.SignInPurpose,
            null,
            now.AddMinutes(5),
            now,
            cancellationToken);

        return new GoogleChallengeDto(issued.RawNonce, issued.ExpiresAt);
    }
}
