using FluentValidation;

namespace Application.Features.TrainerSettings.UpdateContacts;

/// <summary>Valida os limites de comprimento dos campos do contacto.</summary>
public sealed class UpdateContactsCommandValidator : AbstractValidator<UpdateContactsCommand>
{
    public UpdateContactsCommandValidator()
    {
        RuleFor(command => command.Phone)
            .MaximumLength(20)
            .WithErrorCode("trainer_settings_phone_too_long");

        RuleFor(command => command.Address)
            .MaximumLength(500)
            .WithErrorCode("trainer_settings_address_too_long");

        RuleFor(command => command.City)
            .MaximumLength(255)
            .WithErrorCode("trainer_settings_city_too_long");
    }
}
