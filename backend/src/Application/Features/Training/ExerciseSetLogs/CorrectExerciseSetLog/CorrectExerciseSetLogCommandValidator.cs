using FluentValidation;

namespace Application.Features.Training.ExerciseSetLogs.CorrectExerciseSetLog;

/// <summary>Valida uma correção de ClientExerciseSetLog.</summary>
public sealed class CorrectExerciseSetLogCommandValidator : AbstractValidator<CorrectExerciseSetLogCommand>
{
    public CorrectExerciseSetLogCommandValidator()
    {
        RuleFor(command => command.ExerciseSetLogId)
            .NotEmpty().WithErrorCode("exercise_set_log_id_required");

        RuleFor(command => command.WeightKg)
            .GreaterThanOrEqualTo(0).WithErrorCode("training_weight_invalid");

        RuleFor(command => command.RepsDone)
            .InclusiveBetween(0, 100).WithErrorCode("training_reps_done_invalid");

        RuleFor(command => command.Notes)
            .MaximumLength(500).WithErrorCode("training_log_notes_too_long");

        RuleFor(command => command.PerformedAt)
            .NotEmpty().WithErrorCode("training_performed_at_required");
    }
}
