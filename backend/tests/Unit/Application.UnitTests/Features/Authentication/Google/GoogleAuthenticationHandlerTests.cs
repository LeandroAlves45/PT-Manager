using Application.Features.Authentication;
using Application.Features.Authentication.Abstractions;
using Application.Features.Authentication.Google;
using Application.Features.Authentication.Google.Abstractions;
using Application.Features.Authentication.Google.IssueLinkChallenge;
using Application.Features.Authentication.Google.IssueSignInChallenge;
using Application.Features.Authentication.Google.Link;
using Application.Features.Authentication.Google.SignIn;
using Application.Results;
using Domain.Entities.Identity;
using Xunit;

namespace Application.UnitTests.Features.Authentication.Google;

/// <summary>
/// Cobre a orquestração dos quatro casos de uso Google: emissão de challenges,
/// sign-in e linking. As regras transacionais pertencem ao store e são verificadas
/// contra PostgreSQL, não aqui.
/// </summary>
public sealed class GoogleAuthenticationHandlerTests
{
    private static readonly DateTime Now =
        new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SignIn_AuthenticatedOutcome_IssuesApplicationSession()
    {
        var store = new ExternalAuthenticationStoreStub
        {
            SignInResult = GoogleSignInStoreResult.Authenticated(
                new AuthenticatedPrincipal(Guid.NewGuid(), Guid.NewGuid(), "client", "stamp"),
                new IssuedRefreshSession("refresh", "csrf", Now.AddDays(30)))
        };
        var handler = CreateSignIn(store, new ExternalVerifierStub(), new TestEmailSender());

        var result = await handler.HandleAsync(
            new GoogleSignInCommand("id-token", "nonce", null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Session);
        Assert.Equal("access-token", result.Value.Session!.AccessToken);
        Assert.False(result.Value.IsEmailConfirmationRequired);
    }

    [Fact]
    public async Task SignIn_PendingOutcome_SendsConfirmationWithoutSession()
    {
        var sender = new TestEmailSender();
        var store = new ExternalAuthenticationStoreStub
        {
            SignInResult = GoogleSignInStoreResult.ConfirmationRequired(
                new IssuedAuthenticationSecret(
                    "trainer@example.test", "confirmation", Now.AddHours(24)))
        };
        var handler = CreateSignIn(store, new ExternalVerifierStub(), sender);

        var result = await handler.HandleAsync(
            new GoogleSignInCommand("id-token", "nonce", null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsEmailConfirmationRequired);
        Assert.Null(result.Value.Session);
        Assert.Equal(1, sender.ConfirmationCalls);
    }

    [Fact]
    public async Task SignIn_PendingOutcomeWithUnavailableEmail_FailsWithoutSession()
    {
        var sender = new TestEmailSender
        {
            Outcome = AuthenticationEmailDeliveryOutcome.Unavailable
        };
        var store = new ExternalAuthenticationStoreStub
        {
            SignInResult = GoogleSignInStoreResult.ConfirmationRequired(
                new IssuedAuthenticationSecret(
                    "trainer@example.test", "confirmation", Now.AddHours(24)))
        };
        var handler = CreateSignIn(store, new ExternalVerifierStub(), sender);

        var result = await handler.HandleAsync(
            new GoogleSignInCommand("id-token", "nonce", null),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("authentication_email_delivery_unavailable", result.Error!.Code);
    }

    [Fact]
    public async Task SignIn_InvalidCredential_DoesNotCallStore()
    {
        var verifier = new ExternalVerifierStub
        {
            Result = Result<VerifiedExternalIdentity>.Failure(
                GoogleAuthenticationErrors.InvalidCredential)
        };
        var store = new ExternalAuthenticationStoreStub();
        var handler = CreateSignIn(store, verifier, new TestEmailSender());

        var result = await handler.HandleAsync(
            new GoogleSignInCommand("invalid", "nonce", null),
            TestContext.Current.CancellationToken);

        Assert.Equal("google_credential_invalid", result.Error!.Code);
        Assert.Equal(0, store.SignInCalls);
    }

    [Fact]
    public async Task SignIn_AlwaysVerifiesGoogleProviderWithSuppliedNonce()
    {
        // O provider é fixado pelo handler: um cliente nunca pode escolher o emissor.
        var verifier = new ExternalVerifierStub();
        var store = new ExternalAuthenticationStoreStub
        {
            SignInResult = GoogleSignInStoreResult.Failure(
                GoogleSignInStoreStatus.ChallengeInvalid)
        };
        var handler = CreateSignIn(store, verifier, new TestEmailSender());

        await handler.HandleAsync(
            new GoogleSignInCommand("id-token", "nonce-abc", null),
            TestContext.Current.CancellationToken);

        Assert.Equal(ExternalIdentity.GoogleProvider, verifier.ReceivedProvider);
        Assert.Equal("nonce-abc", verifier.ReceivedNonce);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SignIn_BlankInvitationToken_ReachesStoreAsNull(string invitationToken)
    {
        var store = new ExternalAuthenticationStoreStub();
        var handler = CreateSignIn(store, new ExternalVerifierStub(), new TestEmailSender());

        await handler.HandleAsync(
            new GoogleSignInCommand("id-token", "nonce", invitationToken),
            TestContext.Current.CancellationToken);

        Assert.Null(store.ReceivedInvitationToken);
    }

    [Theory]
    [InlineData(GoogleSignInStoreStatus.ChallengeInvalid, "google_credential_invalid")]
    [InlineData(GoogleSignInStoreStatus.AccountLinkRequired, "google_account_link_required")]
    [InlineData(GoogleSignInStoreStatus.AccountInactive, "authentication_account_inactive")]
    [InlineData(GoogleSignInStoreStatus.RelationshipInactive, "authentication_relationship_conflict")]
    [InlineData(GoogleSignInStoreStatus.InvitationInvalid, "authentication_invitation_invalid")]
    [InlineData(GoogleSignInStoreStatus.InvitationExpired, "authentication_invitation_expired")]
    [InlineData(GoogleSignInStoreStatus.InvitationConsumed, "authentication_invitation_consumed")]
    [InlineData(GoogleSignInStoreStatus.InvitationEmailMismatch, "authentication_invitation_email_mismatch")]
    [InlineData(GoogleSignInStoreStatus.RelationshipConflict, "authentication_relationship_conflict")]
    [InlineData(GoogleSignInStoreStatus.ConcurrencyConflict, "authentication_concurrency_conflict")]
    public async Task SignIn_StoreFailure_MapsToStableErrorCode(
        GoogleSignInStoreStatus status,
        string expectedCode)
    {
        var store = new ExternalAuthenticationStoreStub
        {
            SignInResult = GoogleSignInStoreResult.Failure(status)
        };
        var handler = CreateSignIn(store, new ExternalVerifierStub(), new TestEmailSender());

        var result = await handler.HandleAsync(
            new GoogleSignInCommand("id-token", "nonce", null),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error!.Code);
    }

    [Fact]
    public async Task Link_Unauthenticated_DoesNotCallStore()
    {
        var store = new ExternalAuthenticationStoreStub();
        var handler = new GoogleLinkHandler(
            new ValidValidator<GoogleLinkCommand>(),
            new TestTenantContext(),
            new ExternalVerifierStub(),
            store,
            new TestClock(Now));

        var result = await handler.HandleAsync(
            new GoogleLinkCommand("id-token", "nonce", "Password1!"),
            TestContext.Current.CancellationToken);

        Assert.Equal("authentication_account_required", result.Error!.Code);
        Assert.Equal(0, store.LinkCalls);
    }

    [Fact]
    public async Task Link_Authenticated_UsesTenantUserIdNotRequestPayload()
    {
        var userId = Guid.NewGuid();
        var store = new ExternalAuthenticationStoreStub
        {
            LinkResult = GoogleLinkStoreStatus.Linked
        };
        var handler = new GoogleLinkHandler(
            new ValidValidator<GoogleLinkCommand>(),
            new TestTenantContext { UserId = userId, TrainerId = userId, Role = "trainer" },
            new ExternalVerifierStub(),
            store,
            new TestClock(Now));

        var result = await handler.HandleAsync(
            new GoogleLinkCommand("id-token", "nonce", "Password1!"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, store.ReceivedLinkUserId);
    }

    [Fact]
    public async Task Link_InvalidCredential_DoesNotCallStore()
    {
        var verifier = new ExternalVerifierStub
        {
            Result = Result<VerifiedExternalIdentity>.Failure(
                GoogleAuthenticationErrors.EmailNotVerified)
        };
        var store = new ExternalAuthenticationStoreStub();
        var handler = new GoogleLinkHandler(
            new ValidValidator<GoogleLinkCommand>(),
            new TestTenantContext
            {
                UserId = Guid.NewGuid(),
                TrainerId = Guid.NewGuid(),
                Role = "trainer"
            },
            verifier,
            store,
            new TestClock(Now));

        var result = await handler.HandleAsync(
            new GoogleLinkCommand("id-token", "nonce", "Password1!"),
            TestContext.Current.CancellationToken);

        Assert.Equal("google_email_not_verified", result.Error!.Code);
        Assert.Equal(0, store.LinkCalls);
    }

    [Theory]
    [InlineData(GoogleLinkStoreStatus.ChallengeInvalid, "google_credential_invalid")]
    [InlineData(GoogleLinkStoreStatus.UserNotFound, "authentication_account_required")]
    [InlineData(GoogleLinkStoreStatus.PasswordInvalid, "authentication_current_password_invalid")]
    [InlineData(GoogleLinkStoreStatus.EmailMismatch, "google_link_email_mismatch")]
    [InlineData(GoogleLinkStoreStatus.IdentityConflict, "google_identity_conflict")]
    [InlineData(GoogleLinkStoreStatus.ConcurrencyConflict, "authentication_concurrency_conflict")]
    public async Task Link_StoreFailure_MapsToStableErrorCode(
        GoogleLinkStoreStatus status,
        string expectedCode)
    {
        var store = new ExternalAuthenticationStoreStub { LinkResult = status };
        var handler = new GoogleLinkHandler(
            new ValidValidator<GoogleLinkCommand>(),
            new TestTenantContext
            {
                UserId = Guid.NewGuid(),
                TrainerId = Guid.NewGuid(),
                Role = "trainer"
            },
            new ExternalVerifierStub(),
            store,
            new TestClock(Now));

        var result = await handler.HandleAsync(
            new GoogleLinkCommand("id-token", "nonce", "Password1!"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error!.Code);
    }

    [Fact]
    public async Task LinkChallenge_Authenticated_BindsCurrentUser()
    {
        var userId = Guid.NewGuid();
        var store = new ExternalAuthenticationStoreStub();
        var handler = new IssueGoogleLinkChallengeHandler(
            new TestTenantContext { UserId = userId, Role = "trainer", TrainerId = userId },
            store,
            new TestClock(Now));

        var result = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, store.ChallengeUserId);
        Assert.Equal(ExternalAuthenticationChallenge.LinkPurpose, store.ChallengePurpose);
        Assert.Equal(Now.AddMinutes(5), store.ChallengeExpiresAt);
    }

    [Fact]
    public async Task LinkChallenge_Unauthenticated_DoesNotIssue()
    {
        var store = new ExternalAuthenticationStoreStub();
        var handler = new IssueGoogleLinkChallengeHandler(
            new TestTenantContext(),
            store,
            new TestClock(Now));

        var result = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.Equal("authentication_account_required", result.Error!.Code);
        Assert.Equal(0, store.IssueCalls);
    }

    [Fact]
    public async Task SignInChallenge_IssuesAnonymousChallenge()
    {
        var store = new ExternalAuthenticationStoreStub();
        var handler = new IssueGoogleSignInChallengeHandler(store, new TestClock(Now));

        var challenge = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.Equal("nonce", challenge.Nonce);
        Assert.Null(store.ChallengeUserId);
        Assert.Equal(ExternalAuthenticationChallenge.SignInPurpose, store.ChallengePurpose);
        Assert.Equal(Now.AddMinutes(5), challenge.ExpiresAt);
    }

    private static GoogleSignInHandler CreateSignIn(
        ExternalAuthenticationStoreStub store,
        ExternalVerifierStub verifier,
        TestEmailSender sender) =>
        new(
            new ValidValidator<GoogleSignInCommand>(),
            verifier,
            store,
            new AccessTokenIssuerStub(),
            sender,
            new TestClock(Now),
            new AuthenticationPolicy());
}
