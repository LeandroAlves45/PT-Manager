using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.UnitTests.ValueObjects;

/// <summary>Prova a escala do RPE e a lista fechada de grupos musculares (Sprint 6A).</summary>
public sealed class Sprint6AValueRulesTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7.5)]
    [InlineData(10)]
    public void RpeScale_ValuesInHalfSteps_AreValid(double value) =>
        Assert.True(RpeScale.IsValid((decimal)value));

    [Theory]
    [InlineData(0.5)]
    [InlineData(7.25)]
    [InlineData(10.5)]
    public void RpeScale_ValuesOutsideScaleOrStep_AreInvalid(double value) =>
        Assert.False(RpeScale.IsValid((decimal)value));

    [Fact]
    public void RpeScale_EnsureValid_AcceptsNullAndRejectsInvalid()
    {
        RpeScale.EnsureValid(null, "RPE");

        Assert.Throws<DomainException>(() => RpeScale.EnsureValid(8.3m, "RPE"));
    }

    [Fact]
    public void MuscleGroups_Normalize_TrimsLowercasesDeduplicatesAndOrdersCanonically()
    {
        var ok = MuscleGroupCatalog.TryNormalize(" Triceps, chest ,CHEST,,back ", out var normalized);

        Assert.True(ok);
        Assert.Equal("chest,back,triceps", normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" , ")]
    public void MuscleGroups_EmptyInput_NormalizesToNull(string? value)
    {
        Assert.True(MuscleGroupCatalog.TryNormalize(value, out var normalized));
        Assert.Null(normalized);
    }

    [Fact]
    public void MuscleGroups_UnknownCode_IsRejected()
    {
        Assert.False(MuscleGroupCatalog.TryNormalize("chest,costas", out _));
        Assert.Throws<DomainException>(() => MuscleGroupCatalog.Normalize("legs"));
    }
}
