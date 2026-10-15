using Application.Features.Authentication;
using Application.Features.Authentication.Abstractions;
using Application.Features.Authentication.RegisterTrainer;

namespace Application.UnitTests.Features.Authentication;

public sealed class RegistrationHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private static readonly IssuedAuthenticationSecret Secret =
        new("trainer@example.test", "raw", Now.AddHours(24));

    [Fact]
    public async Task Created_SendsConfirmationOnlyAfterStoreSuccess()
    {
        var userId = Guid.NewGuid();
        var store = new RegistrationStoreStub(
            RegisterTrainerStoreResult.Created(userId, userId, Secret));
        var confirmations = NoAccountStore();
        var email = new TestEmailSender();

        var result = await CreateHandler(store, confirmations, email).HandleAsync(
            ValidCommand(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, store.Calls);
        Assert.Equal(1, email.ConfirmationCalls);
        Assert.Equal(0, confirmations.IssueByEmailCalls);
        Assert.Equal(0, email.AccountExistsCalls);
    }

    [Fact]
    public async Task Created_WhenDeliveryFails_StillSucceedsWithoutCompensation()
    {
        var id = Guid.NewGuid();
        var store = new RegistrationStoreStub(
            RegisterTrainerStoreResult.Created(id, id, Secret));
        var email = new TestEmailSender
        {
            Outcome = AuthenticationEmailDeliveryOutcome.Unavailable
        };

        var result = await CreateHandler(store, NoAccountStore(), email).HandleAsync(
            ValidCommand(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, store.Calls);
        Assert.Equal(1, email.ConfirmationCalls);
    }

    [Fact]
    public async Task DuplicateEmail_UnconfirmedAccount_ResendsConfirmation()
    {
        var store = new RegistrationStoreStub(
            RegisterTrainerStoreResult.For(RegisterTrainerStoreStatus.DuplicateEmail));
        var confirmations = new EmailConfirmationStoreStub(
            EmailConfirmationStoreResult.Issued(Secret));
        var email = new TestEmailSender();

        var result = await CreateHandler(store, confirmations, email).HandleAsync(
            ValidCommand(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, confirmations.IssueByEmailCalls);
        Assert.Equal("trainer@example.test", confirmations.LastIssuedEmail);
        Assert.Equal(1, email.ConfirmationCalls);
        Assert.Equal(0, email.AccountExistsCalls);
    }

    [Fact]
    public async Task DuplicateEmail_ConfirmedAccount_SendsAccountAlreadyExistsToTypedEmail()
    {
        var store = new RegistrationStoreStub(
            RegisterTrainerStoreResult.For(RegisterTrainerStoreStatus.DuplicateEmail));
        var confirmations = new EmailConfirmationStoreStub(
            EmailConfirmationStoreResult.For(EmailConfirmationStoreStatus.AlreadyConfirmed));
        var email = new TestEmailSender();

        var result = await CreateHandler(store, confirmations, email).HandleAsync(
            new RegisterTrainerCommand("  trainer@example.test ", "Password1!", "Trainer"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, email.ConfirmationCalls);
        Assert.Equal(1, email.AccountExistsCalls);
        Assert.Equal("trainer@example.test", email.LastAccountExistsRecipient);
    }

    [Theory]
    [InlineData(EmailConfirmationStoreStatus.AccountInactive)]
    [InlineData(EmailConfirmationStoreStatus.UserNotFound)]
    [InlineData(EmailConfirmationStoreStatus.ConcurrencyConflict)]
    public async Task DuplicateEmail_AccountThatCannotBeNotified_SucceedsWithoutEmail(
        EmailConfirmationStoreStatus status)
    {
        var store = new RegistrationStoreStub(
            RegisterTrainerStoreResult.For(RegisterTrainerStoreStatus.DuplicateEmail));
        var email = new TestEmailSender();

        var result = await CreateHandler(
            store,
            new EmailConfirmationStoreStub(EmailConfirmationStoreResult.For(status)),
            email).HandleAsync(ValidCommand(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, email.ConfirmationCalls);
        Assert.Equal(0, email.AccountExistsCalls);
    }

    [Fact]
    public async Task DuplicateEmail_WhenDeliveryFails_StillSucceeds()
    {
        var store = new RegistrationStoreStub(
            RegisterTrainerStoreResult.For(RegisterTrainerStoreStatus.DuplicateEmail));
        var email = new TestEmailSender
        {
            Outcome = AuthenticationEmailDeliveryOutcome.Unavailable
        };

        var result = await CreateHandler(
            store,
            new EmailConfirmationStoreStub(EmailConfirmationStoreResult.For(
                EmailConfirmationStoreStatus.AlreadyConfirmed)),
            email).HandleAsync(ValidCommand(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, email.AccountExistsCalls);
    }

    [Fact]
    public async Task InvalidIdentityData_IsRejectedAsValidationWithoutEmail()
    {
        var store = new RegistrationStoreStub(
            RegisterTrainerStoreResult.For(RegisterTrainerStoreStatus.InvalidIdentityData));
        var email = new TestEmailSender();

        var result = await CreateHandler(store, NoAccountStore(), email).HandleAsync(
            ValidCommand(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Error!.ValidationErrors, error =>
            error.Code == "authentication_registration_rejected");
        Assert.Equal(0, email.ConfirmationCalls);
    }

    [Fact]
    public async Task ConcurrencyConflict_ReturnsStableError()
    {
        var store = new RegistrationStoreStub(
            RegisterTrainerStoreResult.For(RegisterTrainerStoreStatus.ConcurrencyConflict));

        var result = await CreateHandler(store, NoAccountStore(), new TestEmailSender())
            .HandleAsync(ValidCommand(), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.ConcurrencyConflict.Code, result.Error!.Code);
    }

    private static RegisterTrainerCommand ValidCommand() =>
        new("trainer@example.test", "Password1!", "Trainer");

    private static EmailConfirmationStoreStub NoAccountStore() =>
        new(EmailConfirmationStoreResult.For(EmailConfirmationStoreStatus.UserNotFound));

    private static RegisterTrainerHandler CreateHandler(
        RegistrationStoreStub store,
        EmailConfirmationStoreStub confirmations,
        TestEmailSender email) =>
        new(
            new ValidValidator<RegisterTrainerCommand>(),
            new TestClock(Now),
            new AuthenticationPolicy(),
            store,
            confirmations,
            email);
}
