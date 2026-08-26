using Application.Features.Authentication;
using Application.Features.Authentication.Abstractions;
using Application.Features.Authentication.RegisterTrainer;

namespace Application.UnitTests.Features.Authentication;

public sealed class RegistrationHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Created_IsDeliveredOnlyAfterStoreSuccess()
    {
        var userId = Guid.NewGuid();
        var secret = new IssuedAuthenticationSecret(
            "trainer@example.test",
            "raw",
            Now.AddHours(24));
        var store = new RegistrationStoreStub(
            RegisterTrainerStoreResult.Created(userId, userId, secret));
        var email = new TestEmailSender();
        var handler = new RegisterTrainerHandler(
            new ValidValidator<RegisterTrainerCommand>(),
            new TestClock(Now),
            new AuthenticationPolicy(),
            store,
            email);

        var result = await handler.HandleAsync(
            new RegisterTrainerCommand("trainer@example.test", "Password1!", "Trainer"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, store.Calls);
        Assert.Equal(1, email.ConfirmationCalls);
        Assert.Equal(userId, result.Value.TrainerId);
    }

    [Fact]
    public async Task DuplicateEmail_DoesNotCallEmailSender()
    {
        var store = new RegistrationStoreStub(RegisterTrainerStoreResult.For(
            RegisterTrainerStoreStatus.DuplicateEmail));
        var email = new TestEmailSender();
        var handler = new RegisterTrainerHandler(
            new ValidValidator<RegisterTrainerCommand>(),
            new TestClock(Now),
            new AuthenticationPolicy(),
            store,
            email);

        var result = await handler.HandleAsync(
            new RegisterTrainerCommand("trainer@example.test", "Password1!", "Trainer"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.DuplicateEmail.Code, result.Error!.Code);
        Assert.Equal(0, email.ConfirmationCalls);
    }

    [Fact]
    public async Task DeliveryFailure_DoesNotCompensatePersistedAccount()
    {
        var id = Guid.NewGuid();
        var store = new RegistrationStoreStub(RegisterTrainerStoreResult.Created(
            id,
            id,
            new IssuedAuthenticationSecret(
                "trainer@example.test",
                "raw",
                Now.AddHours(24))));
        var email = new TestEmailSender
        {
            Outcome = AuthenticationEmailDeliveryOutcome.Unavailable
        };
        var handler = new RegisterTrainerHandler(
            new ValidValidator<RegisterTrainerCommand>(),
            new TestClock(Now),
            new AuthenticationPolicy(),
            store,
            email);

        var result = await handler.HandleAsync(
            new RegisterTrainerCommand("trainer@example.test", "Password1!", "Trainer"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.EmailDeliveryUnavailable.Code, result.Error!.Code);
        Assert.Equal(1, store.Calls);
    }
}
