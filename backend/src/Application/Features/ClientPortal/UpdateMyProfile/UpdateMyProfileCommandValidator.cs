using Application.Validation;
using FluentValidation;

namespace Application.Features.ClientPortal.UpdateMyProfile;

/// <summary>Valida os campos editáveis do perfil do cliente.</summary>
public sealed class UpdateMyProfileCommandValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileCommandValidator()
    {
        RuleFor(command => command.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("client_name_invalid")
            .WithMessage("Client name must contain between 1 and 255 characters.")
            .Must(name => name.Trim().Length <= 255)
            .WithErrorCode("client_name_invalid")
            .WithMessage("Client name must contain between 1 and 255 characters.")
            .WithoutNullCharacter();

        RuleFor(command => command.Phone)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("client_phone_invalid")
            .MaximumLength(32)
            .WithErrorCode("client_phone_too_long")
            .WithoutNullCharacter();

        RuleFor(command => command.EmergencyContactName)
            .MaximumLength(255)
            .WithErrorCode("client_emergency_contact_name_too_long")
            .WithoutNullCharacter()
            .When(command => !string.IsNullOrWhiteSpace(command.EmergencyContactName));

        RuleFor(command => command.EmergencyContactPhone)
            .MaximumLength(32)
            .WithErrorCode("client_emergency_contact_phone_too_long")
            .WithoutNullCharacter()
            .When(command => !string.IsNullOrWhiteSpace(command.EmergencyContactPhone));
    }
}
