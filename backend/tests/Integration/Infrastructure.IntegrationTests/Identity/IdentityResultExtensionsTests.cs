using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.IntegrationTests.Identity;

public sealed class IdentityResultExtensionsTests
{
    private static readonly IdentityErrorDescriber Errors = new();

    [Fact]
    public void IsOnlyDuplicateAccount_EmailAndUserNameDuplicates_IsTrue()
    {
        var result = IdentityResult.Failed(
            Errors.DuplicateEmail("a@example.test"),
            Errors.DuplicateUserName("a@example.test"));

        Assert.True(result.IsOnlyDuplicateAccount());
    }

    [Fact]
    public void IsOnlyDuplicateAccount_DuplicateMixedWithAnotherError_IsFalse()
    {
        var result = IdentityResult.Failed(
            Errors.DuplicateEmail("a@example.test"),
            Errors.PasswordTooShort(8));

        Assert.False(result.IsOnlyDuplicateAccount());
    }

    [Fact]
    public void IsOnlyDuplicateAccount_PasswordErrorsOnly_IsFalse()
    {
        var result = IdentityResult.Failed(Errors.PasswordRequiresDigit());

        Assert.False(result.IsOnlyDuplicateAccount());
    }

    [Fact]
    public void IsOnlyDuplicateAccount_FailureWithoutErrors_IsFalse()
    {
        Assert.False(IdentityResult.Failed().IsOnlyDuplicateAccount());
    }
}
