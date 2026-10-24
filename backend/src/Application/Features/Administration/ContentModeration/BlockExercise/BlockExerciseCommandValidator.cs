using Application.Validation;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.Features.Administration.ContentModeration.BlockExercise;

/// <summary>Valida o identificador e a allowlist do motivo.</summary>
public sealed class BlockExerciseCommandValidator : AbstractValidator<BlockExerciseCommand>
{
    public BlockExerciseCommandValidator()
    {
        RuleFor(command => command.ExerciseId)
            .NotEmpty()
            .WithErrorCode("exercise_id_required");

        RuleFor(command => command.ReasonCode)
            .Must(PlatformEnforcementReason.IsSupported)
            .WithErrorCode("platform_enforcement_reason_invalid");

        RuleFor(command => command.Note)
            .MaximumLength(PlatformEnforcementNotes.MaxLength)
            .WithErrorCode("platform_enforcement_note_too_long")
            .WithoutNullCharacter();
    }
}
