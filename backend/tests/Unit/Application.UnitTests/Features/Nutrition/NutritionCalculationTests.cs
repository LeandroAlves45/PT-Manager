using Application.Common.Abstractions;
using Application.Features.Nutrition.Calculations;
using Application.Features.Nutrition.PreviewNutrition;
using Domain.Services;
using Domain.ValueObjects;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Features.Nutrition;

public sealed class NutritionCalculationTests
{
    private static readonly DateTime Now = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Preview_ValidFormula_ReturnsServerCalculatedSnapshot()
    {
        // Arrange
        var input = CreateFormulaInput();
        var clock = new TestClock(Now);
        var handler = new PreviewNutritionHandler(
            new PreviewNutritionCommandValidator(),
            clock
        );

        // Act
        var result = await handler.HandleAsync(
            new PreviewNutritionCommand(input),
            TestContext.Current.CancellationToken
        );

        var expectedEnergy = EnergyRequirementCalculator.Calculate(
            new EnergyRequirementInput(
                EnergyFormula.HarrisBenedict,
                input.WeightKg,
                input.HeightCm,
                input.Age!.Value,
                BiologicalSex.Male,
                null,
                ActivityLevel.ModeratelyActive,
                NutritionGoalType.Deficit,
                input.GoalAdjustmentKcal!.Value
            )
        );

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("formula", result.Value.CalculationOrigin);
        Assert.Equal(Now, result.Value.CalculatedAt);
        Assert.Equal(
            decimal.Round(expectedEnergy.TargetKcal, 2, MidpointRounding.AwayFromZero),
            result.Value.TargetKcal
        );
    }

    [Fact]
    public void Preview_DoesNotRequirePersistencePort()
    {
        // Arrange
        var dependencyTypes = typeof(PreviewNutritionHandler)
            .GetConstructors().Single().GetParameters().Select(parameter => parameter.ParameterType);

        // Assert
        Assert.Equal(2, dependencyTypes.Count());
        Assert.DoesNotContain(dependencyTypes, type => type.Name.Contains("Store", StringComparison.Ordinal));
        Assert.DoesNotContain(dependencyTypes, type => type.Name.Contains("Queries", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Validator_FormulaBelowMinimumAge_ReturnsStableFieldError()
    {
        // Arrange
        var validator = new NutritionCalculationInputValidator();
        var result = await validator.TestValidateAsync(
            CreateFormulaInput() with { Age = 17 },
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        result.ShouldHaveValidationErrorFor(input => input.Age)
            .WithErrorCode("nutrition_age_below_minimum");
    }

    [Theory]
    [InlineData(29.99, 40, 30)]
    [InlineData(30.01, 40, 30)]
    public async Task Validator_PercentagesRequireExactHundred(
        decimal protein,
        decimal carbs,
        decimal fats
    )
    {
        // Arrange
        var input = CreateFormulaInput() with
        {
            ProteinPercentage = protein,
            CarbsPercentage = carbs,
            FatsPercentage = fats
        };

        // Act
        var result = await new NutritionCalculationInputValidator()
            .TestValidateAsync(input, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.ErrorCode == "macro_percentage_total_invalid");
    }

    [Theory]
    [InlineData(150, true)]
    [InlineData(149.9975, false)]
    public async Task Validator_ManualDifferenceBoundary_IsCorrect(
        decimal carbsGrams,
        bool expectedValid
    )
    {
        // Arrange
        var input = CreateManualInput() with { CarbsGrams = carbsGrams };

        // Act
        var result = await new NutritionCalculationInputValidator()
            .TestValidateAsync(input, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public async Task Factory_SameInputAndInstant_MatchesPreview()
    {
        // Arrange
        var input = CreateFormulaInput();
        var expected = NutritionCalculationFactory.CreateSnapshot(input, Now);
        var handler = new PreviewNutritionHandler(
            new PreviewNutritionCommandValidator(),
            new TestClock(Now)
        );

        // Act
        var result = await handler.HandleAsync(
            new PreviewNutritionCommand(input),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expected.TargetKcal, result.Value.TargetKcal);
        Assert.Equal(expected.ProteinTargetGrams, result.Value.ProteinTargetGrams);
        Assert.Equal(expected.CalculatedAt, result.Value.CalculatedAt);
    }

    [Fact]
    public async Task Validator_RejectsFieldsFromAnotherDiscriminatorBranch()
    {
        // Arrange
        // EnergyFormula só é rejeitado por "energy_formula_not_allowed" no ramo manual_energy —
        // a partir de um CreateFormulaInput() esta regra nunca dispara (falso positivo silencioso).
        var input = CreateManualInput() with { EnergyFormula = "tinsley" };

        // Act
        var result = await new NutritionCalculationInputValidator()
            .TestValidateAsync(input, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(value => value.EnergyFormula)
            .WithErrorCode("energy_formula_not_allowed");
    }

    private static NutritionCalculationInput CreateFormulaInput() => new(
        "formula",
        "harris_benedict",
        80m,
        180m,
        30,
        "male",
        null,
        "moderately_active",
        "deficit",
        500m,
        null,
        "percentage",
        30m,
        40m,
        30m,
        null,
        null,
        null,
        null,
        null
    );

    private static NutritionCalculationInput CreateManualInput() => new(
        "manual_energy",
        null,
        80m,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        2000m,
        "manual_grams",
        null,
        null,
        null,
        null,
        null,
        100m,
        150m,
        100m
    );

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
