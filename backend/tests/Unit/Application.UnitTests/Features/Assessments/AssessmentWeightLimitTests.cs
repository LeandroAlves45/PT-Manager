using Application.Features.Assessments.CheckIns.CorrectCheckIn;
using Application.Features.Assessments.CheckIns.SubmitCheckInResponse;
using Application.Features.Assessments.InitialAssessments.CreateInitialAssessment;
using Application.Features.Assessments.InitialAssessments.UpdateInitialAssessment;
using Domain.Entities.Assessments;

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
