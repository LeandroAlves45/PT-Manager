using Application.Features.Authentication.Google.Abstractions;
using Application.Results;

namespace Application.UnitTests.Features.Authentication.Google;

/// <summary>
/// Adapter de verificação substituído por um valor fixo. Não reproduz criptografia:
/// a validação real do ID token é coberta nos testes de Infrastructure.
/// </summary>
internal sealed class ExternalVerifierStub : IExternalIdentityVerifier
{
    internal Result<VerifiedExternalIdentity> Result { get; set; } =
        Result<VerifiedExternalIdentity>.Success(new VerifiedExternalIdentity(
            "google", "subject", "trainer@example.test", "Trainer", true));

    internal int Calls { get; private set; }
    internal string? ReceivedProvider { get; private set; }
    internal string? ReceivedNonce { get; private set; }

    public Task<Result<VerifiedExternalIdentity>> VerifyAsync(
        string provider,
        string idToken,
        string expectedNonce,
        CancellationToken cancellationToken)
    {
        Calls++;
        ReceivedProvider = provider;
        ReceivedNonce = expectedNonce;
        return Task.FromResult(Result);
    }
}

/// <summary>
/// Store transacional substituído por resultados fechados. Não reproduz SQL nem as
/// regras de convite, que pertencem ao store real e são testadas em PostgreSQL.
/// </summary>
internal sealed class ExternalAuthenticationStoreStub :
    IExternalAuthenticationStore,
    IExternalChallengeStore
{
    internal GoogleSignInStoreResult SignInResult { get; set; } =
        GoogleSignInStoreResult.Failure(GoogleSignInStoreStatus.ChallengeInvalid);
    internal GoogleLinkStoreStatus LinkResult { get; set; } =
        GoogleLinkStoreStatus.ChallengeInvalid;

    internal int SignInCalls { get; private set; }
    internal int LinkCalls { get; private set; }
    internal int IssueCalls { get; private set; }
    internal Guid? ChallengeUserId { get; private set; }
    internal string? ChallengePurpose { get; private set; }
    internal DateTime ChallengeExpiresAt { get; private set; }
    internal string? ReceivedInvitationToken { get; private set; }
    internal Guid ReceivedLinkUserId { get; private set; }

    public Task<IssuedExternalChallenge> IssueAsync(
        string purpose,
        Guid? userId,
        DateTime expiresAt,
        DateTime now,
        CancellationToken cancellationToken)
    {
        IssueCalls++;
        ChallengePurpose = purpose;
        ChallengeUserId = userId;
        ChallengeExpiresAt = expiresAt;
        return Task.FromResult(new IssuedExternalChallenge("nonce", expiresAt));
    }

    public Task<GoogleSignInStoreResult> SignInAsync(
        VerifiedExternalIdentity identity,
        string rawNonce,
        string? rawInvitationToken,
        DateTime trialEndsAt,
        DateTime confirmationExpiresAt,
        DateTime refreshExpiresAt,
        DateTime now,
        CancellationToken cancellationToken)
    {
        SignInCalls++;
        ReceivedInvitationToken = rawInvitationToken;
        return Task.FromResult(SignInResult);
    }

    public Task<GoogleLinkStoreStatus> LinkAsync(
        Guid userId,
        VerifiedExternalIdentity identity,
        string rawNonce,
        string currentPassword,
        DateTime now,
        CancellationToken cancellationToken)
    {
        LinkCalls++;
        ReceivedLinkUserId = userId;
        return Task.FromResult(LinkResult);
    }
}
