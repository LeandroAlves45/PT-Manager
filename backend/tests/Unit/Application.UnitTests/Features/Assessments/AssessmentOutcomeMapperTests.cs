using Application.Features.Assessments.CheckIns;
using Application.Features.Assessments.CheckIns.Abstractions;
using Application.Features.Assessments.InitialAssessments;
using Application.Features.Assessments.InitialAssessments.Abstractions;
using Domain.Entities.Assessments;
using Domain.ValueObjects;

namespace Application.UnitTests.Features.Assessments;

public sealed class AssessmentOutcomeMapperTests
{
    private static readonly DateTime Now =
        new(2026, 8, 17, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 8, 17);

    [Theory]
    [InlineData(InitialAssessmentStoreResult.Status.ClientNotFound, "client_not_found")]
    [InlineData(InitialAssessmentStoreResult.Status.ClientInactive, "assessment_client_inactive")]
    [InlineData(InitialAssessmentStoreResult.Status.AssessmentNotFound, "initial_assessment_not_found")]
    [InlineData(InitialAssessmentStoreResult.Status.AssessmentAlreadyExists, "initial_assessment_already_exists")]
    public void InitialAssessmentFailure_MapsStableCode(
        InitialAssessmentStoreResult.Status status,
        string expectedCode)
    {
        var result = InitialAssessmentStoreResult.For(status).ToResult();

        Assert.Equal(expectedCode, result.Error!.Code);
    }

    [Theory]
    [InlineData(InitialAssessmentStoreResult.Status.Created)]
    [InlineData(InitialAssessmentStoreResult.Status.Updated)]
    [InlineData(InitialAssessmentStoreResult.Status.AlreadyInRequestedState)]
    public void InitialAssessmentSuccess_MapsEntity(
        InitialAssessmentStoreResult.Status status)
    {
        var assessment = CreateInitialAssessment();

        var result = InitialAssessmentStoreResult.For(status, assessment).ToResult();

        Assert.Equal(assessment.Id, result.Value.Id);
    }

    [Theory]
    [InlineData(CheckInStoreResult.Status.ClientNotFound, "client_not_found")]
    [InlineData(CheckInStoreResult.Status.ClientInactive, "assessment_client_inactive")]
    [InlineData(CheckInStoreResult.Status.CheckInNotFound, "check_in_not_found")]
    [InlineData(CheckInStoreResult.Status.DateConflict, "check_in_date_conflict")]
    [InlineData(CheckInStoreResult.Status.CannotReschedule, "check_in_cannot_be_rescheduled")]
    [InlineData(CheckInStoreResult.Status.CannotCancel, "check_in_cannot_be_cancelled")]
    [InlineData(CheckInStoreResult.Status.WrongResponseDay, "check_in_wrong_day")]
    [InlineData(CheckInStoreResult.Status.AlreadyAnswered, "check_in_already_answered")]
    [InlineData(CheckInStoreResult.Status.CheckInCancelled, "check_in_cancelled")]
    [InlineData(CheckInStoreResult.Status.NotAnswered, "check_in_not_answered")]
    public void CheckInFailure_MapsStableCode(
        CheckInStoreResult.Status status,
        string expectedCode)
    {
        var result = CheckInStoreResult.For(status).ToResult(Today);

        Assert.Equal(expectedCode, result.Error!.Code);
    }

    [Fact]
    public void CheckInDateNotAllowed_MapsFieldValidation()
    {
        var result = CheckInStoreResult.For(
            CheckInStoreResult.Status.DateNotAllowed).ToResult(Today);

        Assert.Contains(
            result.Error!.ValidationErrors,
            error => error.Field == "CheckInDate" &&
                error.Code == "check_in_date_not_allowed");
    }

    [Theory]
    [InlineData(CheckInStoreResult.Status.Created)]
    [InlineData(CheckInStoreResult.Status.Rescheduled)]
    [InlineData(CheckInStoreResult.Status.Cancelled)]
    [InlineData(CheckInStoreResult.Status.Answered)]
    [InlineData(CheckInStoreResult.Status.Corrected)]
    [InlineData(CheckInStoreResult.Status.AlreadyInRequestedState)]
    public void CheckInSuccess_MapsEntity(CheckInStoreResult.Status status)
    {
        var checkIn = new CheckIn(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Today,
            null,
            Now);

        var result = CheckInStoreResult.For(status, checkIn).ToResult(Today);

        Assert.Equal(checkIn.Id, result.Value.Id);
    }

    private static InitialAssessment CreateInitialAssessment() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        80m,
        180,
        null,
        null,
        "intermediate",
        ActivityLevel.ModeratelyActive,
        "strength",
        null,
        BodyMeasurements.Empty,
        NutritionIntake.Empty,
        Now);
}
