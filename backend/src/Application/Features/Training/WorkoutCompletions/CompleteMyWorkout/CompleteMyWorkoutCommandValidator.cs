using Domain.Entities.Training;
using FluentValidation;

namespace Application.Features.Training.WorkoutCompletions.CompleteMyWorkout;

/// <summary>Valida o dia a concluir e as notas opcionais.</summary>
public sealed class CompleteMyWorkoutCommandValidator : AbstractValidator<CompleteMyWorkoutCommand>
{
    public CompleteMyWorkoutCommandValidator()
    {
        RuleFor(command => command.TrainingPlanDayId)
            .NotEmpty()
            .WithErrorCode("training_day_id_required");

        RuleFor(command => command.Notes)
            .MaximumLength(WorkoutCompletion.NotesMaxLength)
            .WithErrorCode("workout_completion_notes_too_long");
    }
}
