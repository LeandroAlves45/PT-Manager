using Application.Common.Abstractions;
using Application.Features.Authentication;
using Application.Features.Authentication.Abstractions;
using Application.Features.Authentication.Login;
using Application.Features.Authentication.Logout;
using Application.Features.Authentication.RefreshSession;

namespace Application.UnitTests.Features.Authentication;

public sealed class SessionHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Login_LockedOut_DoesNotRevealAccountState()
    {
        var store = new SessionStoreStub
        {
            AuthenticateResult = AuthenticateStoreResult.Failure(
                AuthenticateStoreStatus.LockedOut)
        };
        var handler = new LoginHandler(
            new ValidValidator<LoginCommand>(),
            new TestClock(Now),
            new AuthenticationPolicy(),
            store,
            new AccessTokenIssuerStub(),
            new AccessQueriesStub());

        var result = await handler.HandleAsync(
            new LoginCommand("trainer@example.test", "wrong"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.InvalidCredentials.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Login_BlockedTrainer_ReturnsReasonAndNoteAndRevokesSessions()
    {
        var store = AuthenticatedStore("trainer");
        var access = new AccessQueriesStub
        {
            State = new AccountAccessState(
                AccountAccessStatus.TrainerBlocked, "malicious_content", "Conteúdo copiado.")
        };

        var result = await CreateLogin(store, access).HandleAsync(
            new LoginCommand("trainer@example.test", "right"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("trainer_account_blocked", result.Error!.Code);
        Assert.Equal("malicious_content", result.Error.Metadata["block_reason"]);
        Assert.Equal("Conteúdo copiado.", result.Error.Metadata["block_note"]);
        Assert.Equal(1, store.RevokeAllCalls);
    }

    [Fact]
    public async Task Login_ClientAfterAccessPeriod_ReturnsPeriodEnded()
    {
        var store = AuthenticatedStore("client");
        var access = new AccessQueriesStub { State = AccountAccessState.ClientAccessPeriodEnded };

        var result = await CreateLogin(store, access).HandleAsync(
            new LoginCommand("client@example.test", "right"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("client_access_period_ended", result.Error!.Code);
        Assert.Equal(1, store.RevokeAllCalls);
    }

    [Fact]
    public async Task Login_AllowedAccount_IssuesSessionWithoutRevoking()
    {
        var store = AuthenticatedStore("client");

        var result = await CreateLogin(store, new AccessQueriesStub()).HandleAsync(
            new LoginCommand("client@example.test", "right"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, store.RevokeAllCalls);
    }

    private static SessionStoreStub AuthenticatedStore(string role) => new()
    {
        AuthenticateResult = AuthenticateStoreResult.Authenticated(
            new AuthenticatedPrincipal(Guid.NewGuid(), Guid.NewGuid(), role, "stamp"),
            new IssuedRefreshSession("refresh", "csrf", Now.AddDays(30)))
    };

    private static LoginHandler CreateLogin(SessionStoreStub store, AccessQueriesStub access) => new(
        new ValidValidator<LoginCommand>(),
        new TestClock(Now),
        new AuthenticationPolicy(),
        store,
        new AccessTokenIssuerStub(),
        access);

    [Fact]
    public async Task Refresh_ReusedToken_ReturnsGenericInvalidSession()
    {
        var store = new SessionStoreStub
        {
            RotateResult = RotateRefreshStoreResult.Failure(RotateRefreshStoreStatus.Reused)
        };
        var handler = new RefreshSessionHandler(
            new ValidValidator<RefreshSessionCommand>(),
            new TestClock(Now),
            new AuthenticationPolicy(),
            store,
            new AccessTokenIssuerStub());

        var result = await handler.HandleAsync(
            new RefreshSessionCommand("raw", "csrf"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.RefreshSessionInvalid.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Logout_UnknownToken_RemainsIdempotent()
    {
        var store = new SessionStoreStub();
        var handler = new LogoutHandler(
            new ValidValidator<LogoutCommand>(),
            new TestClock(Now),
            store);

        var result = await handler.HandleAsync(
            new LogoutCommand("unknown", "csrf"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, store.RevokeCalls);
    }
}
