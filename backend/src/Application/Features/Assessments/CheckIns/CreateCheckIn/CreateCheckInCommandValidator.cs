using FluentValidation;

namespace Application.Features.Assessments.CheckIns.CreateCheckIn;

/// <summary>Valida o agendamento de CheckIn.</summary>
public sealed class CreateCheckInCommandValidator : AbstractValidator<CreateCheckInCommand>
{
    public CreateCheckInCommandValidator()
    {
        RuleFor(command => command.ClientId)
            .NotEmpty()
            .WithErrorCode("client_id_required");

        RuleFor(command => command.CheckInDate)
            .NotEmpty()
            .WithErrorCode("check_in_date_required");

        RuleFor(command => command)
            .Must(command => !command.TargetDate.HasValue || command.TargetDate.Value >= command.CheckInDate)
            .WithName("TargetDate")
            .WithErrorCode("target_date_before_check_in");
    }
}
