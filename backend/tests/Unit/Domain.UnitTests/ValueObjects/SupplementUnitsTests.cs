using Domain.Entities.Supplements;
using Domain.ValueObjects;
using Xunit;

namespace Domain.UnitTests.ValueObjects;

/// <summary>U60: «gr» conta como «g» e guarda-se sempre «g».</summary>
public sealed class SupplementUnitsTests
{
    [Theory]
    [InlineData("gr", "g")]
    [InlineData("GR", "g")]
    [InlineData("Gr", "g")]
    [InlineData("g", "g")]
    [InlineData("cápsulas", "cápsulas")]
    [InlineData("grama", "grama")]
    public void NormalizeUnit_OnlyReplacesExactGr(string unit, string expected) =>
        Assert.Equal(expected, SupplementUnits.NormalizeUnit(unit));

    [Theory]
    [InlineData("5gr", "5g")]
    [InlineData("5 gr", "5 g")]
    [InlineData("2,5 GR", "2,5 g")]
    [InlineData("5gr por dia", "5g por dia")]
    [InlineData("5 grãos", "5 grãos")]
    [InlineData("5 grams", "5 grams")]
    [InlineData("1 scoop (30 gr)", "1 scoop (30 g)")]
    [InlineData("green tea", "green tea")]
    public void NormalizeServingSize_ReplacesGrAfterNumber(string servingSize, string expected) =>
        Assert.Equal(expected, SupplementUnits.NormalizeServingSize(servingSize));

    [Fact]
    public void Supplement_StoresNormalizedUnitAndServing()
    {
        var supplement = new Supplement(
            Guid.NewGuid(), Guid.NewGuid(), "Creatina", null, "GR", "5 gr", null, null,
            new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(("g", "5 g"), (supplement.UnitOfMeasure, supplement.ServingSize));
    }
}
