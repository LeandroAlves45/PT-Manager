using Domain.ValueObjects;
using Domain.Exceptions;
using Xunit;

namespace Unit.Domain.UnitTests.ValueObjects;

public sealed class MacroSummaryTests
{
    [Fact]
    public void Kcal_ComputesAtwaterFormula()
    {
        // Arrange
        var summary = new MacroSummary(
            proteinGrams: 100,
            carbsGrams: 200,
            fatGrams: 50
        );

        // Act
        var kcal = summary.Kcal;

        // Assert
        Assert.Equal(100 * 4 + 200 * 4 + 50 * 9, kcal);
    }

    [Fact]
    public void Constructor_NegativeMacro_ThrowsDomainException()
    {
        // Arrange & Act
        var exception = Assert.Throws<DomainException>(() =>
            new MacroSummary(-1, 0, 0));

        // Assert
        Assert.Equal("Macro grams cannot be negative.", exception.Message);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var summary1 = new MacroSummary(30, 40, 10);
        var summary2 = new MacroSummary(30, 40, 10);

        // Act & Assert
        Assert.Equal(summary1, summary2);
    }
}
