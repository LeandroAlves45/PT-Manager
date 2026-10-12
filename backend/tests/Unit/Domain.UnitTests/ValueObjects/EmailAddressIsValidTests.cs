using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;

namespace Unit.Domain.UnitTests.ValueObjects;

/// <summary><c>IsValid</c> e o construtor têm de concordar: é isso que evita o 500.</summary>
public sealed class EmailAddressIsValidTests
{
    [Theory]
    [InlineData("joao@ptmanager.local")]
    [InlineData("  Joao@Example.com ")]
    public void IsValid_AcceptedByConstructor_ReturnsTrue(string email)
    {
        Assert.True(EmailAddress.IsValid(email));
        Assert.NotNull(new EmailAddress(email));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a@b")]
    [InlineData("a b@c.pt")]
    [InlineData("a@c.pt.")]
    [InlineData("a@.c.pt")]
    [InlineData("a@b@c.pt")]
    [InlineData("sem-arroba")]
    public void IsValid_RejectedByConstructor_ReturnsFalse(string? email)
    {
        Assert.False(EmailAddress.IsValid(email));
        Assert.Throws<DomainException>(() => new EmailAddress(email!));
    }

    [Fact]
    public void IsValid_LongerThanLimit_ReturnsFalse()
    {
        Assert.False(EmailAddress.IsValid(new string('a', 251) + "@c.pt"));
    }
}
