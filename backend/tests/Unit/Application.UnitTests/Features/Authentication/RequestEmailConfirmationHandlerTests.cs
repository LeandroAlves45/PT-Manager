using Application.Features.Authentication;
using Application.Features.Authentication.Abstractions;
using Application.Features.Authentication.RequestEmailConfirmation;

namespace Application.UnitTests.Features.Authentication;

public sealed class RequestEmailConfirmationHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task UnconfirmedAccount_SendsConfirmationToTheStoredEmail()
    {
        var store = new EmailConfirmationStoreStub(EmailConfirmationStoreResult.Issued(
            new IssuedAuthenticationSecret("trainer@example.test", "raw", Now.AddHours(24))));
        var email = new TestEmailSender();

        var result = await CreateHandler(store, email).HandleAsync(
            new RequestEmailConfirmationCommand(" trainer@example.test "),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("trainer@example.test", store.LastIssuedEmail);
        Assert.Equal(1, email.ConfirmationCalls);
    }

    [Theory]
    [InlineData(EmailConfirmationStoreStatus.UserNotFound)]
    [InlineData(EmailConfirmationStoreStatus.AccountInactive)]
    [InlineData(EmailConfirmationStoreStatus.AlreadyConfirmed)]
    [InlineData(EmailConfirmationStoreStatus.ConcurrencyConflict)]
    public async Task IneligibleAccount_SucceedsWithoutSendingAnything(
        EmailConfirmationStoreStatus status)
    {
        var store = new EmailConfirmationStoreStub(EmailConfirmationStoreResult.For(status));
        var email = new TestEmailSender();

        var result = await CreateHandler(store, email).HandleAsync(
            new RequestEmailConfirmationCommand("trainer@example.test"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, email.ConfirmationCalls);
        Assert.Equal(0, email.AccountExistsCalls);
    }

    [Fact]
    public async Task DeliveryFailure_StillSucceeds()
    {
        var store = new EmailConfirmationStoreStub(EmailConfirmationStoreResult.Issued(
            new IssuedAuthenticationSecret("trainer@example.test", "raw", Now.AddHours(24))));
        var email = new TestEmailSender
        {
            Outcome = AuthenticationEmailDeliveryOutcome.Unavailable
        };

        var result = await CreateHandler(store, email).HandleAsync(
            new RequestEmailConfirmationCommand("trainer@example.test"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, email.ConfirmationCalls);
    }

    [Fact]
    public async Task InvalidEmail_IsRejectedBeforeTheStore()
    {
        var store = new EmailConfirmationStoreStub(
            EmailConfirmationStoreResult.For(EmailConfirmationStoreStatus.UserNotFound));
        var handler = new RequestEmailConfirmationHandler(
            new RequestEmailConfirmationCommandValidator(),
            new TestClock(Now),
            new AuthenticationPolicy(),
            store,
            new TestEmailSender());

        var result = await handler.HandleAsync(
            new RequestEmailConfirmationCommand("a@b"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Error!.ValidationErrors, error =>
            error.Code == "authentication_email_invalid");
        Assert.Equal(0, store.IssueByEmailCalls);
    }

    private static RequestEmailConfirmationHandler CreateHandler(
        EmailConfirmationStoreStub store,
        TestEmailSender email) =>
        new(
            new ValidValidator<RequestEmailConfirmationCommand>(),
            new TestClock(Now),
            new AuthenticationPolicy(),
            store,
            email);
}
