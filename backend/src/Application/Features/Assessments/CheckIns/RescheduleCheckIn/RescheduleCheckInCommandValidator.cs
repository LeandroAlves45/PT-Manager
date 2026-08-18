using FluentValidation;

namespace Application.Features.Assessments.CheckIns.RescheduleCheckIn;

/// <summary>Valida um reagendamento de check-in.</summary>
public sealed class RescheduleCheckInCommandValidator : AbstractValidator<RescheduleCheckInCommand>
{
    public RescheduleCheckInCommandValidator()
    {
        RuleFor(command => command.CheckInId)
            .NotEmpty()
            .WithErrorCode("check_in_id_required");

        RuleFor(command => command.CheckInDate)
            .NotEmpty()
            .WithErrorCode("check_in_date_required");

        RuleFor(command => command)
            .Must(command => !command.TargetDate.HasValue || command.TargetDate.Value >= command.CheckInDate)
            .WithName("TargetDate")
            .WithErrorCode("target_date_before_check_in");
    }
}
