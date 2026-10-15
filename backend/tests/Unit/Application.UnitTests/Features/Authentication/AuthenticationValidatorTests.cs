using Application.Features.Authentication.InviteClient;
using Application.Features.Authentication.Login;
using Application.Features.Authentication.RegisterInvitedClient;
using Application.Features.Authentication.RegisterTrainer;
using Application.Features.Authentication.RequestEmailConfirmation;
using Application.Features.Authentication.RequestPasswordReset;

namespace Application.UnitTests.Features.Authentication;

public sealed class AuthenticationValidatorTests
{
    [Fact]
    public async Task Login_LegacyShortPassword_IsAcceptedByDefensiveValidation()
    {
        var validator = new LoginCommandValidator();

        var result = await validator.ValidateAsync(
            new LoginCommand("trainer@example.test", "short"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Login_PasswordAboveDefensiveLimit_IsRejected()
    {
        var validator = new LoginCommandValidator();

        var result = await validator.ValidateAsync(
            new LoginCommand("trainer@example.test", new string('a', 513)),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(129)]
    public async Task RegisterTrainer_PasswordOutsideCreationPolicy_IsRejected(int length)
    {
        var validator = new RegisterTrainerCommandValidator();

        var result = await validator.ValidateAsync(
            new RegisterTrainerCommand(
                "trainer@example.test",
                new string('a', length),
                "Trainer"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task RegisterTrainer_EmailTheDomainWouldReject_IsRejectedAsValidation()
    {
        var validator = new RegisterTrainerCommandValidator();

        var result = await validator.ValidateAsync(
            new RegisterTrainerCommand("a@b", "Password1!", "Trainer"),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error => error.ErrorCode == "authentication_email_invalid");
    }

    [Theory]
    [InlineData("", "authentication_email_required")]
    [InlineData("not-an-email", "authentication_email_invalid")]
    [InlineData("a@b", "authentication_email_invalid")]
    public async Task RequestEmailConfirmation_InvalidEmail_IsRejected(string email, string code)
    {
        var validator = new RequestEmailConfirmationCommandValidator();

        var result = await validator.ValidateAsync(
            new RequestEmailConfirmationCommand(email),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error => error.ErrorCode == code);
    }

    [Fact]
    public async Task RequestEmailConfirmation_ValidEmail_IsAccepted()
    {
        var validator = new RequestEmailConfirmationCommandValidator();

        var result = await validator.ValidateAsync(
            new RequestEmailConfirmationCommand("trainer@example.test"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "Password1!", "Password1!", "authentication_invite_token_required")]
    [InlineData("token", "", "", "authentication_password_required")]
    [InlineData("token", "short", "short", "authentication_password_too_short")]
    [InlineData("token", "Password1!", "Different1!", "authentication_password_confirmation_mismatch")]
    public async Task RegisterInvitedClient_InvalidInput_IsRejectedWithStableCode(
        string token,
        string password,
        string confirmation,
        string code)
    {
        var validator = new RegisterInvitedClientCommandValidator();

        var result = await validator.ValidateAsync(
            new RegisterInvitedClientCommand(token, password, confirmation),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error => error.ErrorCode == code);
    }

    [Fact]
    public async Task RegisterInvitedClient_PasswordAboveCreationPolicy_IsRejected()
    {
        var validator = new RegisterInvitedClientCommandValidator();
        var password = new string('a', 129);

        var result = await validator.ValidateAsync(
            new RegisterInvitedClientCommand("token", password, password),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error =>
            error.ErrorCode == "authentication_password_too_long");
    }

    [Theory]
    [InlineData("a@b")]
    [InlineData("not-an-email")]
    public async Task InviteClient_EmailTheDomainWouldReject_IsRejectedAsValidation(string email)
    {
        var result = await new InviteClientCommandValidator().ValidateAsync(
            new InviteClientCommand(Guid.NewGuid(), email),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error => error.ErrorCode == "authentication_email_invalid");
    }

    [Fact]
    public async Task InviteClient_ValidEmail_IsAccepted()
    {
        var result = await new InviteClientCommandValidator().ValidateAsync(
            new InviteClientCommand(Guid.NewGuid(), "client@example.test"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Login_EmailTheDomainWouldReject_IsRejectedAsValidation()
    {
        var result = await new LoginCommandValidator().ValidateAsync(
            new LoginCommand("a@b", "Password1!"),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error => error.ErrorCode == "authentication_email_invalid");
    }

    [Fact]
    public async Task RequestPasswordReset_EmailTheDomainWouldReject_IsRejectedAsValidation()
    {
        var result = await new RequestPasswordResetCommandValidator().ValidateAsync(
            new RequestPasswordResetCommand("a@b"),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error => error.ErrorCode == "authentication_email_invalid");
    }
}
