using Application.Features.Nutrition.Foods.CreateFood;
using Application.Features.Nutrition.Foods.CreateGlobalFood;
using Application.Features.Training.ExerciseSetLogs.RecordExerciseSetLog;
using Application.Features.Training.ExerciseSetLogs.RecordMyExerciseSetLog;
using Application.Features.Training.Exercises.CreateExercise;
using Application.Features.Training.TrainingPlans;
using Application.Features.Training.WorkoutCompletions.CompleteMyWorkout;

namespace Application.UnitTests.Features;

/// <summary>Prova os códigos de erro estáveis das regras de entrada da Sprint 6A.</summary>
public sealed class Sprint6AValidatorsTests
{
    [Fact]
    public void Structure_WithInvalidPlannedRpe_ReturnsPlannedRpeCode()
    {
        var input = new TrainingPlanStructureInput([
            new TrainingPlanStructureInput.TrainingDayInput(null, 1, 1, null, [
                new TrainingPlanStructureInput.DayExerciseInput(Guid.NewGuid(), Guid.NewGuid(), 1, null, null, null, [
                    new TrainingPlanStructureInput.ExerciseSetInput(null, 1, 8, 60m, 60, 90, 7.25m)
                ])
            ])
        ]);

        var result = new TrainingPlanStructureValidator(false).Validate(input);

        Assert.Contains(result.Errors, error => error.ErrorCode == "training_planned_rpe_invalid");
    }

    [Fact]
    public void TrainerLog_WithInvalidRpe_ReturnsRpeCode()
    {
        var command = new RecordExerciseSetLogCommand(
            Guid.NewGuid(), 1, 50m, 8, null, DateTimeOffset.UtcNow, Rpe: 12m);

        var result = new RecordExerciseSetLogCommandValidator().Validate(command);

        Assert.Contains(result.Errors, error => error.ErrorCode == "training_rpe_invalid");
    }

    [Fact]
    public void ClientLog_WithValidValues_IsValid()
    {
        var command = new RecordMyExerciseSetLogCommand(Guid.NewGuid(), 1, 50m, 8, 8.5m, null);

        Assert.True(new RecordMyExerciseSetLogCommandValidator().Validate(command).IsValid);
    }

    [Fact]
    public void ClientLog_WithInvalidRpe_ReturnsRpeCode()
    {
        var command = new RecordMyExerciseSetLogCommand(Guid.NewGuid(), 1, 50m, 8, 0m, null);

        var result = new RecordMyExerciseSetLogCommandValidator().Validate(command);

        Assert.Contains(result.Errors, error => error.ErrorCode == "training_rpe_invalid");
    }

    [Fact]
    public void CompleteWorkout_WithEmptyDayAndLongNotes_ReturnsBothCodes()
    {
        var result = new CompleteMyWorkoutCommandValidator()
            .Validate(new CompleteMyWorkoutCommand(Guid.Empty, new string('x', 501)));

        Assert.Contains(result.Errors, error => error.ErrorCode == "training_day_id_required");
        Assert.Contains(result.Errors, error => error.ErrorCode == "workout_completion_notes_too_long");
    }

    [Fact]
    public void Exercise_WithUnknownMuscleGroup_ReturnsInvalidCode()
    {
        var result = new CreateExerciseCommandValidator()
            .Validate(new CreateExerciseCommand("Row", null, "back,costas", null, null, null));

        Assert.Contains(result.Errors, error => error.ErrorCode == "exercise_muscle_groups_invalid");
    }

    [Fact]
    public void Exercise_WithKnownMuscleGroups_IsValid() =>
        Assert.True(new CreateExerciseCommandValidator()
            .Validate(new CreateExerciseCommand("Row", null, "Back, lats", null, null, null))
            .IsValid);

    [Theory]
    [InlineData(0)]
    [InlineData(1000.5)]
    [InlineData(150.123)]
    public void PrivateFood_WithInvalidDefaultServing_ReturnsInvalidCode(double grams)
    {
        var result = new CreateFoodCommandValidator()
            .Validate(new CreateFoodCommand("Rice", null, 7m, 78m, 1m, null, (decimal)grams));

        Assert.Contains(result.Errors, error => error.ErrorCode == "food_default_serving_invalid");
    }

    [Fact]
    public void GlobalFood_WithInvalidDefaultServing_ReturnsOutOfRangeCode()
    {
        var result = new CreateGlobalFoodCommandValidator()
            .Validate(new CreateGlobalFoodCommand("Rice", null, 7m, 78m, 1m, null, 0m));

        Assert.Contains(result.Errors, error => error.ErrorCode == "food_default_serving_out_of_range");
    }
}
