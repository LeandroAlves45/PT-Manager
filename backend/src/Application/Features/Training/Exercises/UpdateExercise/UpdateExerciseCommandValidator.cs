using FluentValidation;

namespace Application.Features.Training.Exercises.UpdateExercise;

/// <summary>Valida identificador e campos editáveis de um exercício.</summary>
public sealed class UpdateExerciseCommandValidator : AbstractValidator<UpdateExerciseCommand>
{
    public UpdateExerciseCommandValidator()
    {
        RuleFor(command => command.ExerciseId)
            .NotEmpty()
            .WithErrorCode("exercise_id_required");

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
            .WithErrorCode("exercise_video_url_too_long")
            .Must(BeAnAbsoluteHttpsUrl)
            .When(command => !string.IsNullOrWhiteSpace(command.VideoUrl))
            .WithErrorCode("exercise_video_url_must_be_https");
    }

    private static bool BeAnAbsoluteHttpsUrl(string? videoUrl) =>
        Uri.TryCreate(videoUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps;
}
