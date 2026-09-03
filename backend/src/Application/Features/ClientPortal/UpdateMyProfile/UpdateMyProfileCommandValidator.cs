using FluentValidation;

namespace Application.Features.ClientPortal.UpdateMyProfile;

/// <summary>Valida os campos editáveis do perfil do cliente.</summary>
public sealed class UpdateMyProfileCommandValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileCommandValidator()
    {
        RuleFor(command => command.ContactEmail)
            .Cascade(CascadeMode.Stop)
            .MaximumLength(255)
            .WithErrorCode("client_email_too_long")
            .EmailAddress()
            .WithErrorCode("client_email_invalid")
            .When(command => !string.IsNullOrWhiteSpace(command.ContactEmail));

        RuleFor(command => command.Phone)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("client_phone_invalid")
            .MaximumLength(32)
            .WithErrorCode("client_phone_too_long");

        RuleFor(command => command.EmergencyContactName)
            .MaximumLength(255)
            .WithErrorCode("client_emergency_contact_name_too_long")
            .When(command => !string.IsNullOrWhiteSpace(command.EmergencyContactName));

        RuleFor(command => command.EmergencyContactPhone)
            .MaximumLength(32)
            .WithErrorCode("client_emergency_contact_phone_too_long")
            .When(command => !string.IsNullOrWhiteSpace(command.EmergencyContactPhone));
    }
}
