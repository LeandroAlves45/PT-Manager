using System.Globalization;
using Application.Features.Assessments.CheckIns.CorrectCheckIn;
using Application.Features.Assessments.CheckIns.SubmitCheckInResponse;
using Application.Features.Assessments.InitialAssessments.CreateInitialAssessment;
using Application.Features.Assessments.InitialAssessments.UpdateInitialAssessment;
using Domain.Entities.Assessments;
using FluentValidation.Results;

namespace Application.UnitTests.Features.Assessments;

/// <summary>
/// O peso corporal tem teto nos quatro validadores que o recebem. Sem ele, um valor acima da
/// precisão da coluna (<c>numeric(10,2)</c>) chegava à base de dados e devolvia 500.
/// </summary>
public sealed class AssessmentWeightLimitTests
{
    private const decimal AboveMaximum = AssessmentLimits.MaxWeightKg + 0.01m;

    [Fact]
    public void SubmitResponse_WeightAboveMaximum_ReturnsStableCode()
    {
        var result = new SubmitCheckInResponseCommandValidator().Validate(Submit(AboveMaximum));

        Assert.Contains(result.Errors, error =>
            error.PropertyName == "WeightKg" && error.ErrorCode == "weight_invalid");
    }

    [Fact]
    public void SubmitResponse_WeightAtMaximum_Passes()
    {
        var result = new SubmitCheckInResponseCommandValidator()
            .Validate(Submit(AssessmentLimits.MaxWeightKg));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CorrectCheckIn_WeightAboveMaximum_ReturnsStableCode()
    {
        var result = new CorrectCheckInCommandValidator().Validate(Correct(AboveMaximum));

        Assert.Contains(result.Errors, error =>
            error.PropertyName == "WeightKg" && error.ErrorCode == "weight_invalid");
    }

    [Fact]
    public void CorrectCheckIn_WeightAtMaximum_Passes()
    {
        var result = new CorrectCheckInCommandValidator()
            .Validate(Correct(AssessmentLimits.MaxWeightKg));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateInitialAssessment_WeightAboveMaximum_ReturnsStableCode()
    {
        var result = new CreateInitialAssessmentCommandValidator()
            .Validate(CreateInitial(AboveMaximum));

        Assert.Contains(result.Errors, error =>
            error.PropertyName == "WeightKg" && error.ErrorCode == "weight_invalid");
    }

    [Fact]
    public void CreateInitialAssessment_WeightAtMaximum_Passes()
    {
        var result = new CreateInitialAssessmentCommandValidator()
            .Validate(CreateInitial(AssessmentLimits.MaxWeightKg));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateInitialAssessment_WeightAboveMaximum_ReturnsStableCode()
    {
        var result = new UpdateInitialAssessmentCommandValidator()
            .Validate(UpdateInitial(AboveMaximum));

        Assert.Contains(result.Errors, error =>
            error.PropertyName == "WeightKg" && error.ErrorCode == "weight_invalid");
    }

    [Fact]
    public void UpdateInitialAssessment_WeightAtMaximum_Passes()
    {
        var result = new UpdateInitialAssessmentCommandValidator()
            .Validate(UpdateInitial(AssessmentLimits.MaxWeightKg));

        Assert.True(result.IsValid);
    }

    // Valores literais: se a constante do domínio mudasse, estes testes tinham de falhar.
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1000.01")]
    [InlineData("0.004")]
    [InlineData("71.555")]
    public void EveryValidator_RejectsWeightOutOfRangeOrWithMoreThanTwoDecimals(string weight)
    {
        var weightKg = decimal.Parse(weight, CultureInfo.InvariantCulture);

        Assert.All(Validate(weightKg, null), result => Assert.Contains(result.Errors, error =>
            error.PropertyName == "WeightKg" && error.ErrorCode == "weight_invalid"));
    }

    [Theory]
    [InlineData("0.01", null)]
    [InlineData("71.55", null)]
    [InlineData("1000", null)]
    [InlineData("70", "0.01")]
    [InlineData("70", "17.55")]
    [InlineData("70", "99.99")]
    public void EveryValidator_AcceptsValuesInRangeWithUpToTwoDecimals(string weight, string? bodyFat)
    {
        var results = Validate(
            decimal.Parse(weight, CultureInfo.InvariantCulture),
            bodyFat is null ? null : decimal.Parse(bodyFat, CultureInfo.InvariantCulture));

        Assert.All(results, result => Assert.True(result.IsValid));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("100")]
    [InlineData("99.996")]
    [InlineData("17.555")]
    public void EveryValidator_RejectsBodyFatOutOfRangeOrWithMoreThanTwoDecimals(string bodyFat)
    {
        var results = Validate(70m, decimal.Parse(bodyFat, CultureInfo.InvariantCulture));

        Assert.All(results, result => Assert.Contains(result.Errors, error =>
            error.PropertyName == "BodyFatPercentage" &&
            error.ErrorCode == "body_fat_percentage_invalid"));
    }

    private static ValidationResult[] Validate(decimal weightKg, decimal? bodyFat) =>
    [
        new SubmitCheckInResponseCommandValidator()
            .Validate(Submit(weightKg) with { BodyFatPercentage = bodyFat }),
        new CorrectCheckInCommandValidator()
            .Validate(Correct(weightKg) with { BodyFatPercentage = bodyFat }),
        new CreateInitialAssessmentCommandValidator()
            .Validate(CreateInitial(weightKg) with { BodyFatPercentage = bodyFat }),
        new UpdateInitialAssessmentCommandValidator()
            .Validate(UpdateInitial(weightKg) with { BodyFatPercentage = bodyFat })
    ];

    private static SubmitCheckInResponseCommand Submit(decimal weightKg) =>
        new(Guid.NewGuid(), weightKg, null, null, null, null, null, null);

    private static CorrectCheckInCommand Correct(decimal weightKg) =>
        new(Guid.NewGuid(), null, weightKg, null, null, null, null, null, null);

    private static CreateInitialAssessmentCommand CreateInitial(decimal weightKg) =>
        new(Guid.NewGuid(), weightKg, 180, null, null, "intermediate", "moderately_active",
            "strength", null, null, null);

    private static UpdateInitialAssessmentCommand UpdateInitial(decimal weightKg) =>
        new(Guid.NewGuid(), weightKg, 180, null, null, "intermediate", "moderately_active",
            "strength", null, null, null);
}
