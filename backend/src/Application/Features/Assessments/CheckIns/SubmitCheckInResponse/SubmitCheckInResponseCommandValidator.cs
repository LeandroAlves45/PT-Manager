using FluentValidation;

namespace Application.Features.Assessments.CheckIns.SubmitCheckInResponse;

/// <summary>Valida a resposta enviada pelo cliente.</summary>
public sealed class SubmitCheckInResponseCommandValidator : AbstractValidator<SubmitCheckInResponseCommand>
{
    public SubmitCheckInResponseCommandValidator()
    {
        RuleFor(command => command.CheckInId)
            .NotEmpty()
            .WithErrorCode("check_in_id_required");

        RuleFor(command => command.WeightKg)
            .GreaterThan(0)
            .WithErrorCode("weight_invalid");

        RuleFor(command => command.BodyFatPercentage)
            .Must(value => !value.HasValue || value.Value is > 0 and < 100)
            .WithErrorCode("body_fat_percentage_invalid");

        RuleFor(command => command.Notes)
            .MaximumLength(2000)
            .WithErrorCode("notes_too_long");

        RuleFor(command => command.TrainingAdherenceScore)
            .InclusiveBetween(0, 100)
            .When(command => command.TrainingAdherenceScore.HasValue)
            .WithErrorCode("training_adherence_invalid");

        RuleFor(command => command.NutritionAdherenceScore)
            .InclusiveBetween(0, 100)
            .When(command => command.NutritionAdherenceScore.HasValue)
            .WithErrorCode("nutrition_adherence_invalid");

        RuleFor(command => command.BodyMeasurements!)
            .SetValidator(new BodyMeasurementsInputValidator())
            .When(command => command.BodyMeasurements is not null);

        RuleFor(command => command.Feedback!)
            .SetValidator(new CheckInFeedbackInputValidator())
            .When(command => command.Feedback is not null);
    }
}
