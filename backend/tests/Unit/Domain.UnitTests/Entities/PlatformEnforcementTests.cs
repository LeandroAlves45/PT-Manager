using Domain.Entities.Nutrition;
using Domain.Entities.Training;
using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.UnitTests.Entities;

public sealed class PlatformEnforcementTests
{
    private static readonly DateTime Now = new(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Food_BlockSameReason_IsIdempotent()
    {
        var food = CreatePrivateFood();
        food.Block(PlatformEnforcementReason.MaliciousContent, Now);

        var changed = food.Block(PlatformEnforcementReason.MaliciousContent, Now.AddMinutes(1));

        Assert.False(changed);
    }

    [Fact]
    public void Food_BlockSameReason_KeepsOriginalTimestamp()
    {
        var food = CreatePrivateFood();
        food.Block(PlatformEnforcementReason.MaliciousContent, Now);

        food.Block(PlatformEnforcementReason.MaliciousContent, Now.AddMinutes(1));

        Assert.Equal(Now, food.PlatformEnforcedAt);
    }

    [Fact]
    public void Food_BlockDifferentReason_UpdatesDecision()
    {
        var food = CreatePrivateFood();
        food.Block(PlatformEnforcementReason.MaliciousContent, Now);

        food.Block(PlatformEnforcementReason.ProhibitedContent, Now.AddMinutes(1));

        Assert.Equal(PlatformEnforcementReason.ProhibitedContent, food.PlatformEnforcementReason);
    }

    [Fact]
    public void Food_BlockDifferentReason_UpdatesTimestamp()
    {
        var food = CreatePrivateFood();
        food.Block(PlatformEnforcementReason.MaliciousContent, Now);

        food.Block(PlatformEnforcementReason.ProhibitedContent, Now.AddMinutes(1));

        Assert.Equal(Now.AddMinutes(1), food.PlatformEnforcedAt);
    }

    [Fact]
    public void Food_Block_DoesNotChangeTrainerAvailability()
    {
        var food = CreatePrivateFood();

        food.Block(PlatformEnforcementReason.MaliciousContent, Now);

        Assert.True(food.IsActive);
    }

    [Fact]
    public void Food_Unblock_DoesNotReactivateArchivedContent()
    {
        var food = CreatePrivateFood();
        food.SetActive(false, Now);
        food.Block(PlatformEnforcementReason.DangerousInformation, Now);

        food.Unblock(Now.AddMinutes(1));

        Assert.False(food.IsActive);
    }

    [Fact]
    public void Food_Unblock_ClearsReasonAndTimestamp()
    {
        var food = CreatePrivateFood();
        food.Block(PlatformEnforcementReason.DangerousInformation, Now);

        food.Unblock(Now.AddMinutes(1));

        Assert.Equal(
            (PlatformEnforcementStatus.Allowed, (PlatformEnforcementReason?)null, (DateTime?)null),
            (food.PlatformEnforcementStatus, food.PlatformEnforcementReason, food.PlatformEnforcedAt));
    }

    [Fact]
    public void GlobalFood_Block_ThrowsDomainException()
    {
        var food = new Food(null, "Food", null, 1, 1, 1, null, Now);

        void Action() => food.Block(PlatformEnforcementReason.MaliciousContent, Now);

        Assert.Throws<DomainException>(Action);
    }

    [Fact]
    public void Exercise_UnblockRepeated_IsIdempotent()
    {
        var exercise = CreatePrivateExercise();

        var changed = exercise.Unblock(Now);

        Assert.False(changed);
    }

    [Fact]
    public void Exercise_Block_SetsBlockedState()
    {
        var exercise = CreatePrivateExercise();

        var changed = exercise.Block(PlatformEnforcementReason.ProhibitedContent, Now);

        Assert.Equal(
            (true, PlatformEnforcementStatus.Blocked, (DateTime?)Now),
            (changed, exercise.PlatformEnforcementStatus, exercise.PlatformEnforcedAt));
    }

    [Fact]
    public void GlobalExercise_Block_ThrowsDomainException()
    {
        var exercise = new Exercise(null, "Exercise", null, null, null, null, null, Now);

        void Action() => exercise.Block(PlatformEnforcementReason.MaliciousContent, Now);

        Assert.Throws<DomainException>(Action);
    }

    [Theory]
    [InlineData("malicious_content")]
    [InlineData("dangerous_information")]
    [InlineData("deliberately_false_information")]
    [InlineData("prohibited_content")]
    public void Reason_AllowlistedCode_IsSupported(string code)
    {
        Assert.True(PlatformEnforcementReason.IsSupported(code));
    }

    [Theory]
    [InlineData("free_text")]
    [InlineData("")]
    [InlineData(null)]
    public void Reason_UnknownCode_IsNotSupported(string? code)
    {
        Assert.False(PlatformEnforcementReason.IsSupported(code));
    }

    [Fact]
    public void Reason_UnknownCode_FromStringThrows()
    {
        void Action() => PlatformEnforcementReason.FromString("free_text");

        Assert.Throws<DomainException>(Action);
    }

    [Fact]
    public void Status_UnknownValue_FromStringThrows()
    {
        void Action() => PlatformEnforcementStatus.FromString("quarantined");

        Assert.Throws<DomainException>(Action);
    }

    private static Food CreatePrivateFood() =>
        new(Guid.NewGuid(), "Food", null, 1, 1, 1, null, Now);

    private static Exercise CreatePrivateExercise() =>
        new(Guid.NewGuid(), "Exercise", null, null, null, null, null, Now);
}
