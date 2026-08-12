using FluentValidation;

namespace Application.Features.Training.Exercises.CreateExercise;

/// <summary>Valida os campos de criação de um exercício privado.</summary>
public sealed class CreateExerciseCommandValidator : AbstractValidator<CreateExerciseCommand>
{
    /// <summary>Configura os erros por campo com código estáveis.</summary>
    public CreateExerciseCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .WithErrorCode("exercise_name_required")
            .MaximumLength(255)
            .WithErrorCode("exercise_name_too_long");

        RuleFor(command => command.MuscleGroups)
            .MaximumLength(500)
            .WithErrorCode("exercise_muscle_groups_too_long");

        RuleFor(command => command.Equipment)
            .MaximumLength(255)
            .WithErrorCode("exercise_equipment_too_long");

        RuleFor(command => command.DifficultyLevel)
            .MaximumLength(50)
            .WithErrorCode("exercise_difficulty_too_long");

        RuleFor(command => command.VideoUrl)
            .MaximumLength(500)
            .WithErrorCode("exercise_video_url_too_long");
    }
}
