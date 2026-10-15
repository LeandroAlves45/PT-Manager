using Application.Features.Authentication;
using Application.Features.Authentication.Abstractions;
using Application.Features.Authentication.RegisterInvitedClient;

namespace Application.UnitTests.Features.Authentication;

public sealed class RegisterInvitedClientHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Registered_IssuesApplicationSessionForTheClient()
    {
        var principal = new AuthenticatedPrincipal(Guid.NewGuid(), Guid.NewGuid(), "client", "stamp");
        var store = new ClientInvitationStoreStub
        {
            RegisterResult = RegisterInvitedClientStoreResult.Registered(
                principal,
                new IssuedRefreshSession("refresh", "csrf", Now.AddDays(30)))
        };

        var result = await CreateHandler(store).HandleAsync(
            new RegisterInvitedClientCommand("token", "Password1!", "Password1!"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(principal.UserId, result.Value.UserId);
        Assert.Equal("client", result.Value.Role);
        Assert.Equal("access-token", result.Value.AccessToken);
        Assert.Equal("refresh", result.Value.RawRefreshToken);
        Assert.Equal("Password1!", store.LastRegisteredPassword);
    }

    [Theory]
    [InlineData(RegisterInvitedClientStoreStatus.TokenNotFound, "authentication_invitation_invalid")]
    [InlineData(RegisterInvitedClientStoreStatus.TokenExpired, "authentication_invitation_expired")]
    [InlineData(RegisterInvitedClientStoreStatus.TokenAlreadyConsumed, "authentication_invitation_consumed")]
    [InlineData(RegisterInvitedClientStoreStatus.AccountAlreadyExists, "authentication_invitation_account_exists")]
    [InlineData(RegisterInvitedClientStoreStatus.RelationshipConflict, "authentication_relationship_conflict")]
    [InlineData(RegisterInvitedClientStoreStatus.ConcurrencyConflict, "authentication_concurrency_conflict")]
    public async Task StoreFailure_MapsToStableErrorCode(
        RegisterInvitedClientStoreStatus status,
        string expectedCode)
    {
        var store = new ClientInvitationStoreStub
        {
            RegisterResult = RegisterInvitedClientStoreResult.For(status)
        };

        var result = await CreateHandler(store).HandleAsync(
            new RegisterInvitedClientCommand("token", "Password1!", "Password1!"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error!.Code);
    }

    [Fact]
    public async Task PasswordRejected_IsAValidationErrorOnPassword()
    {
        var store = new ClientInvitationStoreStub
        {
            RegisterResult = RegisterInvitedClientStoreResult.For(
                RegisterInvitedClientStoreStatus.PasswordRejected)
        };

        var result = await CreateHandler(store).HandleAsync(
            new RegisterInvitedClientCommand("token", "password", "password"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Error!.ValidationErrors, error =>
            error.Code == "authentication_registration_rejected");
    }

    [Fact]
    public async Task InvalidCommand_NeverReachesTheStore()
    {
        var store = new ClientInvitationStoreStub();
        var handler = new RegisterInvitedClientHandler(
            new RegisterInvitedClientCommandValidator(),
            new TestClock(Now),
            new AuthenticationPolicy(),
            store,
            new AccessTokenIssuerStub());

        var result = await handler.HandleAsync(
            new RegisterInvitedClientCommand("token", "Password1!", "Different1!"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(0, store.RegisterCalls);
        Assert.Contains(result.Error!.ValidationErrors, error =>
            error.Code == "authentication_password_confirmation_mismatch");
    }

    private static RegisterInvitedClientHandler CreateHandler(ClientInvitationStoreStub store) =>
        new(
            new ValidValidator<RegisterInvitedClientCommand>(),
            new TestClock(Now),
            new AuthenticationPolicy(),
            store,
            new AccessTokenIssuerStub());
}
