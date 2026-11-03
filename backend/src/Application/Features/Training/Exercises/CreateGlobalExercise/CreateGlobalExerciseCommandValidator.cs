using Application.Validation;
using FluentValidation;

namespace Application.Features.Training.Exercises.CreateGlobalExercise;

/// <summary>Valida os campos do novo exercício global, incluindo o vídeo HTTPS.</summary>
public sealed class CreateGlobalExerciseCommandValidator
    : AbstractValidator<CreateGlobalExerciseCommand>
{
    public CreateGlobalExerciseCommandValidator()
    {
        RuleFor(command => command.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("exercise_name_required")
            .MaximumLength(255)
            .WithErrorCode("exercise_name_too_long");

        RuleFor(command => command.MuscleGroups)
            .MaximumLength(500)
            .WithErrorCode("exercise_muscle_groups_too_long")
            .MustBeKnownMuscleGroups();

        RuleFor(command => command.Equipment)
            .MaximumLength(255)
            .WithErrorCode("exercise_equipment_too_long");

        RuleFor(command => command.DifficultyLevel)
            .MaximumLength(50)
            .WithErrorCode("exercise_difficulty_too_long");

        RuleFor(command => command.VideoUrl)
            .MustBeExternalVideoUrl();
    }
}
