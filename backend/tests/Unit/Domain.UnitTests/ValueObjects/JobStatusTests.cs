using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;

namespace Unit.Domain.UnitTests.ValueObjects;

public sealed class JobStatusTests
{
    public static TheoryData<string, string> ValidTransitions =>
        new()
        {
            { "pending", "processing" },
            { "processing", "completed" },
            { "processing", "failed" },
            { "processing", "pending" },
            { "failed", "pending" },
            { "failed", "dead_letter" }
        };

    public static TheoryData<string, string> InvalidNonTerminalTransitions =>
        new()
        {
            { "pending", "completed" },
            { "pending", "failed" },
            { "pending", "dead_letter" },
            { "processing", "dead_letter" },
            { "failed", "completed" },
            { "failed", "processing" }
        };

    public static TheoryData<string, string> TerminalTransitions
    {
        get
        {
            var data = new TheoryData<string, string>();
            var terminalStates = new[] { "completed", "dead_letter" };
            var allStates = new[]
            {
                "pending",
                "processing",
                "completed",
                "failed",
                "dead_letter"
            };
            foreach (var current in terminalStates)
            {
                foreach (var next in allStates)
                {
                    data.Add(current, next);
                }
            }
            return data;
        }
    }

    public static TheoryData<string, JobStatus> KnownStatusValuesData =>
        new()
        {
            { "pending", JobStatus.Pending },
            { "processing", JobStatus.Processing },
            { "completed", JobStatus.Completed },
            { "failed", JobStatus.Failed },
            { "dead_letter", JobStatus.DeadLetter }
        };

    [Theory]
    [MemberData(nameof(ValidTransitions))]
    public void CanTransitionTo_ValidTransitions_ReturnsTrue(string current, string next)
    {
        // Arrange
        var currentStatus = JobStatus.FromString(current);
        var nextStatus = JobStatus.FromString(next);

        // Assert
        Assert.True(currentStatus.CanTransitionTo(nextStatus));
    }

    [Theory]
    [MemberData(nameof(InvalidNonTerminalTransitions))]
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
    [MemberData(nameof(TerminalTransitions))]
    public void CanTransitionTo_FromTerminalState_ReturnsFalse(string current, string next)
    {
        // Arrange
        var currentStatus = JobStatus.FromString(current);
        var nextStatus = JobStatus.FromString(next);

        // Assert
        Assert.False(currentStatus.CanTransitionTo(nextStatus));
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
        var exception = Assert.Throws<DomainException>(() => JobStatus.FromString("unknown"));

        // Assert
        Assert.Equal("Invalid job status: unknown", exception.Message);
    }
}
