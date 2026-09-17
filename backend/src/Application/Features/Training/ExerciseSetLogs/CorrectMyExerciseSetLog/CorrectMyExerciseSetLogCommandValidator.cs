using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.ExerciseSetLogs.CorrectMyExerciseSetLog;

/// <summary>Valida a correção de uma série própria.</summary>
public sealed class CorrectMyExerciseSetLogCommandValidator
    : AbstractValidator<CorrectMyExerciseSetLogCommand>
{
    public CorrectMyExerciseSetLogCommandValidator()
    {
        RuleFor(command => command.ExerciseSetLogId)
            .NotEmpty()
            .WithErrorCode("exercise_set_log_id_required");

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
