using Application.Features.Authentication;
using Application.Features.Authentication.Abstractions;
using Application.Features.Authentication.ChangePassword;
using Application.Features.Authentication.ResetPassword;

namespace Application.UnitTests.Features.Authentication;

public sealed class PasswordHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ChangePassword_WithoutAuthenticatedUser_DoesNotCallStore()
    {
        var store = new PasswordStoreStub(PasswordManagementStoreResult.Changed());
        var handler = new ChangePasswordHandler(
            new ValidValidator<ChangePasswordCommand>(),
            new TestTenantContext(),
            new TestClock(Now),
            store);

        var result = await handler.HandleAsync(
            new ChangePasswordCommand("Current1!", "NewPassword1!", "NewPassword1!"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.AuthenticatedAccountRequired.Code, result.Error!.Code);
        Assert.Equal(0, store.ChangeCalls);
    }

    [Fact]
    public async Task ChangePassword_InvalidCurrentPassword_ReturnsUnauthorizedError()
    {
        var store = new PasswordStoreStub(PasswordManagementStoreResult.Failure(
            PasswordManagementStoreStatus.CurrentPasswordInvalid));
        var handler = new ChangePasswordHandler(
            new ValidValidator<ChangePasswordCommand>(),
            new TestTenantContext
            {
                UserId = Guid.NewGuid(),
                Role = "trainer"
            },
            new TestClock(Now),
            store);

        var result = await handler.HandleAsync(
            new ChangePasswordCommand("Current1!", "NewPassword1!", "NewPassword1!"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.CurrentPasswordInvalid.Code, result.Error!.Code);
    }

    [Fact]
    public async Task ResetPassword_ConsumedToken_ReturnsGenericInvalidToken()
    {
        var store = new PasswordStoreStub(PasswordManagementStoreResult.Failure(
            PasswordManagementStoreStatus.ResetTokenConsumed));
        var handler = new ResetPasswordHandler(
            new ValidValidator<ResetPasswordCommand>(),
            new TestClock(Now),
            store);

        var result = await handler.HandleAsync(
            new ResetPasswordCommand("raw", "NewPassword1!", "NewPassword1!"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthenticationErrors.PasswordResetInvalid.Code, result.Error!.Code);
    }
}
