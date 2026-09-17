using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.ExerciseSetLogs.RecordMyExerciseSetLog;

/// <summary>Valida uma série registada pelo cliente com os mesmos limites do personal trainer.</summary>
public sealed class RecordMyExerciseSetLogCommandValidator
    : AbstractValidator<RecordMyExerciseSetLogCommand>
{
    public RecordMyExerciseSetLogCommandValidator()
    {
        RuleFor(command => command.TrainingPlanDayExerciseId)
            .NotEmpty()
            .WithErrorCode("training_day_exercise_id_required");

        RuleFor(command => command.SetNumber)
            .InclusiveBetween(1, 15)
            .WithErrorCode("training_set_number_invalid");

        RuleFor(command => command.WeightKg)
            .GreaterThanOrEqualTo(0)
            .WithErrorCode("training_weight_invalid");

        RuleFor(command => command.RepsDone)
            .InclusiveBetween(0, 100)
            .WithErrorCode("training_reps_done_invalid");

        RuleFor(command => command.Rpe)
            .MustBeValidRpe("training_rpe_invalid");

        RuleFor(command => command.Notes)
            .MaximumLength(500)
            .WithErrorCode("training_log_notes_too_long");
    }
}
