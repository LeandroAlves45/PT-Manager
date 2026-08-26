using Application.Features.Authentication.Login;
using Application.Features.Authentication.RegisterTrainer;

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
}
