using Application.Features.Assessments;
using Application.Features.Assessments.CheckIns.CorrectCheckIn;
using Application.Features.Assessments.CheckIns.CreateCheckIn;
using Application.Features.Assessments.CheckIns.ListCheckIns;
using Application.Features.Assessments.CheckIns.RescheduleCheckIn;
using Application.Features.Assessments.CheckIns.SubmitCheckInResponse;
using Application.Features.Assessments.InitialAssessments.CreateInitialAssessment;
using Application.Features.Assessments.InitialAssessments.UpdateInitialAssessment;

namespace Application.UnitTests.Features.Assessments;

public sealed class AssessmentValidatorsTests
{
    private static readonly DateOnly Today = new(2026, 8, 17);

    public static TheoryData<CreateInitialAssessmentCommand, string>
        InvalidInitialAssessmentCommands => new()
        {
            { ValidInitialCreate() with { ClientId = Guid.Empty }, "client_id_required" },
            { ValidInitialCreate() with { WeightKg = 0m }, "weight_invalid" },
            { ValidInitialCreate() with { HeightCm = 0 }, "height_invalid" },
            { ValidInitialCreate() with { BodyFatPercentage = 100m }, "body_fat_percentage_invalid" },
            { ValidInitialCreate() with { FitnessLevel = string.Empty }, "fitness_level_invalid" },
            { ValidInitialCreate() with { ActivityLevel = "unknown" }, "activity_level_invalid" },
            { ValidInitialCreate() with { Goals = string.Empty }, "goals_required" },
            { ValidInitialCreate() with { Profession = new string('a', 256) }, "profession_too_long" }
        };

    public static TheoryData<AssessmentValueInput.NutritionIntake, string>
        InvalidNutritionInputs => new()
        {
            { Nutrition(foodPreferences: new string('a', 2001)), "nutrition_text_too_long" },
            { Nutrition(sleepQuality: 0), "sleep_quality_invalid" },
            { Nutrition(mood: 6), "mood_invalid" },
            { Nutrition(stressLevel: 0), "stress_level_invalid" },
            { Nutrition(avgWaterLitersPerDay: 0m), "average_water_invalid" }
        };

    public static TheoryData<SubmitCheckInResponseCommand, string>
        InvalidSubmitCommands => new()
        {
            { ValidSubmit() with { CheckInId = Guid.Empty }, "check_in_id_required" },
            { ValidSubmit() with { WeightKg = 0m }, "weight_invalid" },
            { ValidSubmit() with { BodyFatPercentage = 100m }, "body_fat_percentage_invalid" },
            { ValidSubmit() with { Notes = new string('a', 2001) }, "notes_too_long" },
            { ValidSubmit() with { TrainingAdherenceScore = -1 }, "training_adherence_invalid" },
            { ValidSubmit() with { NutritionAdherenceScore = 101 }, "nutrition_adherence_invalid" },
            {
                ValidSubmit() with
                {
                    BodyMeasurements = new AssessmentValueInput.BodyMeasurements(
                        0m, null, null, null, null, null, null, null, null)
                },
                "body_measurement_invalid"
            },
            {
                ValidSubmit() with
                {
                    Feedback = new AssessmentValueInput.CheckInFeedback(
                        new string('a', 2001), null, null, null, null, null)
                },
                "check_in_feedback_too_long"
            }
        };

    [Theory]
    [MemberData(nameof(InvalidInitialAssessmentCommands))]
    public async Task CreateInitialAssessment_InvalidInput_ReturnsStableCode(
        CreateInitialAssessmentCommand command,
        string expectedCode)
    {
        var result = await new CreateInitialAssessmentCommandValidator().ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error => error.ErrorCode == expectedCode);
    }

    [Theory]
    [MemberData(nameof(InvalidNutritionInputs))]
    public async Task CreateInitialAssessment_InvalidNutrition_ReturnsStableCode(
        AssessmentValueInput.NutritionIntake nutrition,
        string expectedCode)
    {
        var result = await new CreateInitialAssessmentCommandValidator().ValidateAsync(
            ValidInitialCreate() with { NutritionIntake = nutrition },
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error => error.ErrorCode == expectedCode);
    }

    [Theory]
    [MemberData(nameof(InvalidSubmitCommands))]
    public async Task SubmitResponse_InvalidInput_ReturnsStableCode(
        SubmitCheckInResponseCommand command,
        string expectedCode)
    {
        var result = await new SubmitCheckInResponseCommandValidator().ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error => error.ErrorCode == expectedCode);
    }

    [Theory]
    [InlineData(0, "weight_invalid")]
    [InlineData(-1, "weight_invalid")]
    public async Task CreateInitialAssessment_InvalidWeight_ReturnsStableCode(
        decimal weightKg,
        string expectedCode)
    {
        var result = await new CreateInitialAssessmentCommandValidator().ValidateAsync(
            ValidInitialCreate() with { WeightKg = weightKg },
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error => error.ErrorCode == expectedCode);
    }

    [Fact]
    public async Task CreateInitialAssessment_InvalidActivityLevel_ReturnsStableCode()
    {
        var result = await new CreateInitialAssessmentCommandValidator().ValidateAsync(
            ValidInitialCreate() with { ActivityLevel = "unknown" },
            TestContext.Current.CancellationToken);

        Assert.Contains(
            result.Errors,
            error => error.ErrorCode == "activity_level_invalid");
    }

    [Fact]
    public async Task CreateInitialAssessment_InvalidMeasurement_ReturnsStableCode()
    {
        var result = await new CreateInitialAssessmentCommandValidator().ValidateAsync(
            ValidInitialCreate() with
            {
                BodyMeasurements = new AssessmentValueInput.BodyMeasurements(
                    0m, null, null, null, null, null, null, null, null)
            },
            TestContext.Current.CancellationToken);

        Assert.Contains(
            result.Errors,
            error => error.ErrorCode == "body_measurement_invalid");
    }

    [Fact]
    public async Task CreateInitialAssessment_InvalidNutritionValue_ReturnsStableCode()
    {
        var result = await new CreateInitialAssessmentCommandValidator().ValidateAsync(
            ValidInitialCreate() with
            {
                NutritionIntake = new AssessmentValueInput.NutritionIntake(
                    null, null, null, null, null, null, 0, null, null,
                    null, null, null, null, null)
            },
            TestContext.Current.CancellationToken);

        Assert.Contains(
            result.Errors,
            error => error.ErrorCode == "sleep_quality_invalid");
    }

    [Fact]
    public async Task UpdateInitialAssessment_EmptyId_ReturnsStableCode()
    {
        var command = new UpdateInitialAssessmentCommand(
            Guid.Empty,
            80m,
            180,
            null,
            null,
            "intermediate",
            "moderately_active",
            "strength",
            null,
            null,
            null);

        var result = await new UpdateInitialAssessmentCommandValidator().ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.Contains(
            result.Errors,
            error => error.ErrorCode == "initial_assessment_id_required");
    }

    [Fact]
    public async Task CreateCheckIn_TargetBeforeCheckIn_ReturnsStableCode()
    {
        var command = new CreateCheckInCommand(
            Guid.NewGuid(),
            Today,
            Today.AddDays(-1));

        var result = await new CreateCheckInCommandValidator().ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.Contains(
            result.Errors,
            error => error.ErrorCode == "target_date_before_check_in");
    }

    [Theory]
    [InlineData(true, false, "client_id_required")]
    [InlineData(false, true, "check_in_date_required")]
    public async Task CreateCheckIn_RequiredValueMissing_ReturnsStableCode(
        bool emptyClientId,
        bool emptyDate,
        string expectedCode)
    {
        var command = new CreateCheckInCommand(
            emptyClientId ? Guid.Empty : Guid.NewGuid(),
            emptyDate ? default : Today,
            null);

        var result = await new CreateCheckInCommandValidator().ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error => error.ErrorCode == expectedCode);
    }

    [Fact]
    public async Task RescheduleCheckIn_EmptyId_ReturnsStableCode()
    {
        var command = new RescheduleCheckInCommand(Guid.Empty, Today, null);

        var result = await new RescheduleCheckInCommandValidator().ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.Contains(
            result.Errors,
            error => error.ErrorCode == "check_in_id_required");
    }

    [Fact]
    public async Task RescheduleCheckIn_EmptyDate_ReturnsStableCode()
    {
        var command = new RescheduleCheckInCommand(Guid.NewGuid(), default, null);

        var result = await new RescheduleCheckInCommandValidator().ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.Contains(
            result.Errors,
            error => error.ErrorCode == "check_in_date_required");
    }

    [Fact]
    public async Task RescheduleCheckIn_TargetBeforeDate_ReturnsStableCode()
    {
        var command = new RescheduleCheckInCommand(
            Guid.NewGuid(),
            Today,
            Today.AddDays(-1));

        var result = await new RescheduleCheckInCommandValidator().ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.Contains(
            result.Errors,
            error => error.ErrorCode == "target_date_before_check_in");
    }

    [Fact]
    public async Task ListCheckIns_ReversedRange_ReturnsStableCode()
    {
        var query = new ListCheckInsQuery(
            null,
            null,
            Today,
            Today.AddDays(-1));

        var result = await new ListCheckInsQueryValidator().ValidateAsync(
            query,
            TestContext.Current.CancellationToken);

        Assert.Contains(
            result.Errors,
            error => error.ErrorCode == "check_in_date_range_invalid");
    }

    [Theory]
    [InlineData(0, 50, "page_number_invalid")]
    [InlineData(1, 101, "page_size_invalid")]
    public async Task ListCheckIns_InvalidPagination_ReturnsStableCode(
        int pageNumber,
        int pageSize,
        string expectedCode)
    {
        var query = new ListCheckInsQuery(
            null,
            null,
            null,
            null,
            pageNumber,
            pageSize);

        var result = await new ListCheckInsQueryValidator().ValidateAsync(
            query,
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error => error.ErrorCode == expectedCode);
    }

    [Fact]
    public async Task ListCheckIns_EmptyClientId_ReturnsStableCode()
    {
        var query = new ListCheckInsQuery(Guid.Empty, null, null, null);

        var result = await new ListCheckInsQueryValidator().ValidateAsync(
            query,
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error => error.ErrorCode == "client_id_invalid");
    }

    [Fact]
    public async Task SubmitResponse_InvalidFeedback_ReturnsStableCode()
    {
        var command = ValidSubmit() with
        {
            Feedback = new AssessmentValueInput.CheckInFeedback(
                new string('a', 2001), null, null, null, null, null)
        };

        var result = await new SubmitCheckInResponseCommandValidator().ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.Contains(
            result.Errors,
            error => error.ErrorCode == "check_in_feedback_too_long");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task SubmitResponse_InvalidAdherence_ReturnsStableCode(int score)
    {
        var result = await new SubmitCheckInResponseCommandValidator().ValidateAsync(
            ValidSubmit() with { TrainingAdherenceScore = score },
            TestContext.Current.CancellationToken);

        Assert.Contains(
            result.Errors,
            error => error.ErrorCode == "training_adherence_invalid");
    }

    [Fact]
    public async Task CorrectCheckIn_ZeroWeight_ReturnsStableCode()
    {
        var command = new CorrectCheckInCommand(
            Guid.NewGuid(),
            null,
            0m,
            null,
            null,
            null,
            null,
            null,
            null);

        var result = await new CorrectCheckInCommandValidator().ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error => error.ErrorCode == "weight_invalid");
    }

    [Fact]
    public async Task CorrectCheckIn_EmptyId_ReturnsStableCode()
    {
        var command = new CorrectCheckInCommand(
            Guid.Empty,
            null,
            80m,
            null,
            null,
            null,
            null,
            null,
            null);

        var result = await new CorrectCheckInCommandValidator().ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.Contains(
            result.Errors,
            error => error.ErrorCode == "check_in_id_required");
    }

    private static CreateInitialAssessmentCommand ValidInitialCreate() => new(
        Guid.NewGuid(),
        80m,
        180,
        null,
        null,
        "intermediate",
        "moderately_active",
        "strength",
        null,
        null,
        null);

    private static SubmitCheckInResponseCommand ValidSubmit() => new(
        Guid.NewGuid(),
        80m,
        null,
        null,
        null,
        null,
        null,
        null);

    private static AssessmentValueInput.NutritionIntake Nutrition(
        string? foodPreferences = null,
        int? sleepQuality = null,
        int? mood = null,
        int? stressLevel = null,
        decimal? avgWaterLitersPerDay = null) => new(
        foodPreferences,
        null,
        null,
        null,
        null,
        null,
        sleepQuality,
        mood,
        stressLevel,
        avgWaterLitersPerDay,
        null,
        null,
        null,
        null);
}
