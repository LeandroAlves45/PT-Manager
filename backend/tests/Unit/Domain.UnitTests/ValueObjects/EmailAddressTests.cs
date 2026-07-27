using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;

namespace Unit.Domain.UnitTests.ValueObjects;

public sealed class EmailAddressTests
{
    [Fact]
    public void Constructor_ValidEmail_NormalizesToUpperInvariant()
    {
        // Arrange & Act
        var email = new EmailAddress("  Joao@Example.com ");

        // Assert
        Assert.Equal("JOAO@EXAMPLE.COM", email.Normalized);
    }

    [Theory]
    [InlineData("without-at-sign")]
    [InlineData("two@@atsign.com")]
    [InlineData("invalid@domain")]
    [InlineData("with space@x.com")]
    public void Constructor_InvalidFormat_ThrowsDomainException(string invalidEmail)
    {
        // Arrange & Act
        var exception = Assert.Throws<DomainException>(() => new EmailAddress(invalidEmail));

        // Assert
        Assert.Equal("Email with the invalid format.", exception.Message);
    }

    [Fact]
    public void Constructor_EmptyEmail_ThrowsDomainException()
    {
        // Arrange & Act
        var exception = Assert.Throws<DomainException>(() => new EmailAddress(string.Empty));

        // Assert
        Assert.Equal("Email cannot be empty.", exception.Message);
    }

    [Fact]
    public void Constructor_TooLong_Email_ThrowsDomainException()
    {
        // Arrange
        var longEmail = new string('a', 256) + "@example.com";

        // Act
        var exception = Assert.Throws<DomainException>(() => new EmailAddress(longEmail));

        // Assert
        Assert.Equal("Email with the invalid format.", exception.Message);
    }
}
