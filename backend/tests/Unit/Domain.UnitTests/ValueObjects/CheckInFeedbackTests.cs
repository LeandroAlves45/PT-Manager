using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;

namespace Domain.UnitTests.ValueObjects;

public sealed class CheckInFeedbackTests
{
    [Fact]
    public void Constructor_AllFieldsNull_IsValidState()
    {
        // Act
        var feedback = new CheckInFeedback(null, null, null, null, null, null);

        // Assert
        Assert.Equal(CheckInFeedback.Empty, feedback);
    }

    [Fact]
    public void Constructor_TextWithinLimit_IsValidState()
    {
        // Arrange
        var text = new string('a', 2000);

        // Act
        var feedback = new CheckInFeedback(text, null, null, null, null, null);

        // Assert
        Assert.Equal(text, feedback.Appetite);
    }

    [Fact]
    public void Constructor_TextExceedsLimit_ThrowsException()
    {
        // Arrange
        var text = new string('a', 2001);

        // Act & Assert
        Assert.Throws<DomainException>(() => new CheckInFeedback(text, null, null, null, null, null));
    }

    [Fact]
    public void Constructor_Text_TrimsOuterWhitespace()
    {
        // Arrange
        var feedback = new CheckInFeedback("  Some feedback  ", null, null, null, null, null);

        // Assert
        Assert.Equal("Some feedback", feedback.Appetite);
    }
}
