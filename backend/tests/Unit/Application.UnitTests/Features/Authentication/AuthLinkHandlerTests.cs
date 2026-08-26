using Application.Features.Authentication;
using Application.Features.Authentication.Abstractions;
using Application.Features.Authentication.ConfirmEmail;
using Application.Features.Authentication.InviteClient;
using Application.Features.Authentication.RequestPasswordReset;

namespace Application.UnitTests.Features.Authentication;

public sealed class AuthLinkHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ConfirmEmail_ExpiredToken_ReturnsStableError()
    {
        var handler = new ConfirmEmailHandler(
            new ValidValidator<ConfirmEmailCommand>(),
            new TestClock(Now),
            new EmailConfirmationStoreStub(EmailConfirmationStoreResult.For(
                EmailConfirmationStoreStatus.TokenExpired)));

        var result = await handler.HandleAsync(
            new ConfirmEmailCommand("raw"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.ConfirmationTokenExpired.Code, result.Error!.Code);
    }

    [Fact]
    public async Task RequestReset_IneligibleAccount_PreservesAntiEnumerationResponse()
    {
        var email = new TestEmailSender();
        var handler = new RequestPasswordResetHandler(
            new ValidValidator<RequestPasswordResetCommand>(),
            new TestClock(Now),
            new AuthenticationPolicy(),
            new PasswordResetRequestStoreStub(PasswordResetRequestStoreResult.For(
                PasswordResetRequestStoreStatus.NotEligible)),
            email);

        var result = await handler.HandleAsync(
            new RequestPasswordResetCommand("missing@example.test"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, email.ResetCalls);
    }

    [Fact]
    public async Task RequestReset_EmailDeliveryUnavailable_PreservesAntiEnumerationResponse()
    {
        var secret = new IssuedAuthenticationSecret(
            "trainer@example.test",
            "raw",
            Now.AddHours(1));
        var email = new TestEmailSender
        {
            Outcome = AuthenticationEmailDeliveryOutcome.Unavailable
        };
        var handler = new RequestPasswordResetHandler(
            new ValidValidator<RequestPasswordResetCommand>(),
            new TestClock(Now),
            new AuthenticationPolicy(),
            new PasswordResetRequestStoreStub(
                PasswordResetRequestStoreResult.Issued(secret)),
            email);

        var result = await handler.HandleAsync(
            new RequestPasswordResetCommand("trainer@example.test"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, email.ResetCalls);
    }

    [Fact]
    public async Task InviteClient_ClientActor_IsRejectedBeforePersistence()
    {
        var store = new ClientInvitationStoreStub();
        var handler = new InviteClientHandler(
            new ValidValidator<InviteClientCommand>(),
            new TestTenantContext
            {
                TrainerId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Role = "client"
            },
            new TestClock(Now),
            new AuthenticationPolicy(),
            store,
            new TestEmailSender());

        var result = await handler.HandleAsync(
            new InviteClientCommand(Guid.NewGuid(), "client@example.test"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.TrainerOnly.Code, result.Error!.Code);
        Assert.Equal(0, store.IssueCalls);
    }
}
