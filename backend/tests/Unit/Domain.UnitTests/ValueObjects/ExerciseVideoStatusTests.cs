using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.UnitTests.ValueObjects;

/// <summary>Congela a máquina de estados técnica do vídeo gerido.</summary>
public sealed class ExerciseVideoStatusTests
{
    public static TheoryData<string, string, bool> Transitions() => new()
    {
        { "pending", "processing", true },
        { "pending", "rejected", true },
        { "pending", "failed", true },
        { "pending", "ready", false },
        { "processing", "ready", true },
        { "processing", "rejected", true },
        { "processing", "failed", true },
        { "processing", "pending", false },
        { "ready", "processing", false },
        { "ready", "rejected", false },
        { "rejected", "processing", false },
        { "failed", "pending", false }
    };

    [Theory]
    [MemberData(nameof(Transitions))]
    public void CanTransitionTo_MatchesTheApprovedLifecycle(string from, string to, bool expected)
    {
        Assert.Equal(
            expected,
            ExerciseVideoStatus.FromString(from).CanTransitionTo(ExerciseVideoStatus.FromString(to)));
    }

    [Fact]
    public void Flags_SeparateInFlightFromTerminal()
    {
        Assert.True(ExerciseVideoStatus.Pending.IsInFlight);
        Assert.True(ExerciseVideoStatus.Processing.IsInFlight);
        Assert.All(
            new[] { ExerciseVideoStatus.Ready, ExerciseVideoStatus.Rejected, ExerciseVideoStatus.Failed },
            status => Assert.True(status.IsTerminal && !status.IsInFlight));
    }

    [Fact]
    public void KnownStatuses_CannotBeReassigned()
    {
        var fields = typeof(ExerciseVideoStatus)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.FieldType == typeof(ExerciseVideoStatus))
            .ToArray();

        Assert.Equal(5, fields.Length);
        Assert.All(fields, field => Assert.True(field.IsInitOnly, field.Name));
    }

    [Theory]
    [InlineData("Ready")]
    [InlineData("deleted")]
    [InlineData("")]
    public void FromString_RejectsUnknownValues(string value)
    {
        Assert.Throws<DomainException>(() => ExerciseVideoStatus.FromString(value));
    }
}
