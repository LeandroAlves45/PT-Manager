using Domain.ValueObjects;
using Domain.Exceptions;
using Xunit;
namespace Unit.Domain.UnitTests.ValueObjects;

public sealed class JobStatusTests
{
    [Theory]
    [InlineData("pending", "processing")]
    [InlineData("processing", "completed")]
    [InlineData("processing", "failed")]
    [InlineData("failed", "pending")]
    [InlineData("failed", "dead_letter")]
    public void CanTransitionTo_ValidTransitions_ReturnsTrue(string current, string next)
    {
        // Arrange
        var currentStatus = JobStatus.FromString(current);
        var nextStatus = JobStatus.FromString(next);

        // Act
        var result = currentStatus.CanTransitionTo(nextStatus);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("pending", "completed")]
    [InlineData("pending", "failed")]
    [InlineData("pending", "dead_letter")]
    [InlineData("processing", "pending")]
    [InlineData("processing", "dead_letter")]
    [InlineData("failed", "completed")]
    [InlineData("failed", "processing")]
    public void CanTransitionTo_IllegalNonTerminalTransitions_ReturnsFalse(string current, string next)
    {
        // Arrange
        var currentStatus = JobStatus.FromString(current);
        var nextStatus = JobStatus.FromString(next);

        // Act
        var result = currentStatus.CanTransitionTo(nextStatus);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("completed", "pending")]
    [InlineData("completed", "processing")]
    [InlineData("completed", "failed")]
    [InlineData("completed", "dead_letter")]
    [InlineData("dead_letter", "pending")]
    [InlineData("dead_letter", "processing")]
    [InlineData("dead_letter", "completed")]
    [InlineData("dead_letter", "failed")]
    public void CanTransitionTo_FromTerminalState_AlwaysReturnsFalse(string current, string next)
    {
        // Arrange
        var currentStatus = JobStatus.FromString(current);
        var nextStatus = JobStatus.FromString(next);

        // Act
        var result = currentStatus.CanTransitionTo(nextStatus);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [MemberData(nameof(KnownStatusValuesData))]
    public void FromString_KnownValue_ReturnsCorrespondingSingleton(string value, JobStatus expected)
    {
        // Act
        var result = JobStatus.FromString(value);

        // Assert
        Assert.Same(expected, result);
    }

    [Fact]
    public void FromString_UnknownValue_ThrowsDomainException()
    {
        // Arrange & Act
        var exception = Assert.Throws<DomainException>(() => JobStatus.FromString("bogus"));

        // Assert
        Assert.Equal("Invalid job status: bogus", exception.Message);
    }

    /// <summary>
    /// Helper para fornecer os valores conhecidos do JobStatus para os testes.
    /// </summary>
    public static TheoryData<string, JobStatus> KnownStatusValuesData =>
        new()
        {
            { "pending", JobStatus.Pending },
            { "processing", JobStatus.Processing },
            { "completed", JobStatus.Completed },
            { "failed", JobStatus.Failed },
            { "dead_letter", JobStatus.DeadLetter }
        };
}
