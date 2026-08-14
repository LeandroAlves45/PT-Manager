using FluentValidation;

namespace Application.Features.Training.ExerciseSetLogs.RecordExerciseSetLog;

/// <summary>Valida valores executados e instante obrigatório.</summary>
public sealed class RecordExerciseSetLogCommandValidator : AbstractValidator<RecordExerciseSetLogCommand>
{
    public RecordExerciseSetLogCommandValidator()
    {
        RuleFor(command => command.TrainingPlanDayExerciseId)
            .NotEmpty().WithErrorCode("training_day_exercise_id_required");

        RuleFor(command => command.SetNumber)
            .InclusiveBetween(1, 15).WithErrorCode("training_set_number_invalid");

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
