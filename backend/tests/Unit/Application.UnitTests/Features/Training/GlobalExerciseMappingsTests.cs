using Application.Features.Training;
using Application.Features.Training.Exercises;
using Application.Features.Training.Exercises.Abstractions;
using Application.Features.Training.Exercises.CreateExercise;
using Application.Features.Training.Exercises.UpdateExercise;
using Domain.Entities.Training;

namespace Application.UnitTests.Features.Training;

public sealed class GlobalExerciseMappingsTests
{
    private static readonly DateTime Now = new(2026, 8, 21, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ToDtoResult_WhenCreated_ReturnsGlobalProjection()
    {
        var exercise = new Exercise(null, "Squat", null, "legs", "barbell", "medium", null, Now);
        var outcome = GlobalExerciseStoreResult.WithExercise(
            GlobalExerciseStoreResult.Status.Created, exercise);

        var result = outcome.ToDtoResult();

        Assert.Equal("Squat", result.Value.Name);
    }

    [Theory]
    [InlineData(GlobalExerciseStoreResult.Status.NotFound, "exercise_not_found")]
    [InlineData(GlobalExerciseStoreResult.Status.Inactive, "exercise_inactive")]
    [InlineData(GlobalExerciseStoreResult.Status.Referenced, "global_exercise_referenced")]
    public void ToDtoResult_WhenStoreRejectsMutation_ReturnsFeatureError(
        GlobalExerciseStoreResult.Status status, string expectedCode)
    {
        var result = GlobalExerciseStoreResult.For(status).ToDtoResult();

        Assert.Equal(expectedCode, result.Error!.Code);
    }

    [Fact]
    public void ToTransitionResult_WhenHasReferences_ReturnsDeleteConflict()
    {
        var result = GlobalExerciseStoreResult.For(
            GlobalExerciseStoreResult.Status.HasReferences).ToTransitionResult();

        Assert.Equal(TrainingErrors.GlobalExerciseHasReferences.Code, result.Error!.Code);
    }

    [Theory]
    [InlineData(GlobalExerciseStoreResult.Status.Changed)]
    [InlineData(GlobalExerciseStoreResult.Status.Deleted)]
    [InlineData(GlobalExerciseStoreResult.Status.AlreadyInRequestedState)]
    public void ToTransitionResult_WhenTransitionSucceeded_ReturnsSuccess(
        GlobalExerciseStoreResult.Status status)
    {
        var result = GlobalExerciseStoreResult.For(status).ToTransitionResult();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CreatePrivateExerciseValidator_WhenVideoUsesHttp_ReturnsHttpsError()
    {
        var validator = new CreateExerciseCommandValidator();
        var command = new CreateExerciseCommand(
            "Squat", null, null, null, null, "http://example.com/video");

        var result = await validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors,
            failure => failure.ErrorCode == "exercise_video_url_must_be_https");
    }

    [Fact]
    public async Task UpdatePrivateExerciseValidator_WhenVideoUsesHttps_IsValid()
    {
        var validator = new UpdateExerciseCommandValidator();
        var command = new UpdateExerciseCommand(
            Guid.NewGuid(), "Squat", null, null, null, null, "https://example.com/video");

        var result = await validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }
}
